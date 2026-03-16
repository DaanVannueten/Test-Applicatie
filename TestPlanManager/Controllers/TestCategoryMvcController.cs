using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TestPlanManager.Data;
using TestPlanManager.Models;

namespace TestPlanManager.Controllers
{
    [Authorize]
    public class TestCategoryMvcController : Controller
    {
        private readonly TestPlanContext _ctx;
        private readonly IWebHostEnvironment _environment;
        private static readonly HashSet<string> AllowedMediaExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".mp4", ".webm", ".mov", ".avi"
        };

        private const long MaxMediaFileSizeBytes = 50 * 1024 * 1024;

        public TestCategoryMvcController(TestPlanContext ctx, IWebHostEnvironment environment)
        {
            _ctx = ctx;
            _environment = environment;
        }

        // list of all categories (maybe redirect to dashboard)
        public IActionResult Index()
        {
            return RedirectToAction("Index", "Home");
        }

        public async Task<IActionResult> Details(int id)
        {
            var cat = await _ctx.TestCategories
                .Include(tc => tc.Sprint)
                .Include(tc => tc.Tests)
                .FirstOrDefaultAsync(tc => tc.TestCategoryId == id);
            if (cat == null) return NotFound();
            cat.Recalculate();
            return View(cat);
        }

        // action to edit test status
        public async Task<IActionResult> EditTest(int id)
        {
            var test = await _ctx.Tests
                .Include(t => t.TestCategory)
                .ThenInclude(tc => tc.Sprint)
                .FirstOrDefaultAsync(t => t.TestId == id);
            if (test == null) return NotFound();
            return View(test);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTest(Test model, IFormFile? mediaFile, bool removeMedia = false)
        {
            var test = await _ctx.Tests.FindAsync(model.TestId);
            if (test == null) return NotFound();

            var previousStatus = test.ExecutionStatus;

            var category = await _ctx.TestCategories
                .Include(tc => tc.Tests)
                .FirstOrDefaultAsync(tc => tc.TestCategoryId == model.TestCategoryId);
            if (category == null) return NotFound();

            test.Name = TestTitleSanitizer.Clean(model.Name);
            test.ScopeStatus = model.ScopeStatus;
            test.ExecutionStatus = model.ExecutionStatus;
            test.Production = model.Production ?? "";
            test.Comments = model.Comments ?? "";

            if (removeMedia)
            {
                DeleteMediaFile(test.MediaUrl);
                test.MediaUrl = null;
            }

            if (mediaFile != null && mediaFile.Length > 0)
            {
                var uploadResult = await SaveMediaFileAsync(mediaFile);
                if (!uploadResult.Succeeded)
                {
                    ModelState.AddModelError("", uploadResult.ErrorMessage!);
                    model.MediaUrl = test.MediaUrl;
                    model.TestCategory = category;
                    return View(model);
                }

                DeleteMediaFile(test.MediaUrl);
                test.MediaUrl = uploadResult.MediaUrl;
            }

            if (model.ExecutionStatus != Models.ExecutionStatus.NotRun)
            {
                if (previousStatus == Models.ExecutionStatus.NotRun || !test.ExecutedAt.HasValue)
                {
                    test.ExecutedAt = DateTime.UtcNow;
                }

                test.LastExecutedBy = User.Identity?.Name;
            }
            else
            {
                test.ExecutedAt = null;
                test.LastExecutedBy = null;
            }

            category.TestDate = category.Tests
                .Where(t => t.ExecutionStatus != Models.ExecutionStatus.NotRun && t.ExecutedAt.HasValue)
                .Select(t => t.ExecutedAt)
                .Max();

            _ctx.Tests.Update(test);
            await _ctx.SaveChangesAsync();
            var detailsUrl = Url.Action("Details", new { id = model.TestCategoryId });
            return Redirect($"{detailsUrl}#test-{model.TestId}");
        }

        [HttpGet]
        public async Task<IActionResult> CreateTest(int testCategoryId)
        {
            var category = await _ctx.TestCategories
                .Include(tc => tc.Sprint)
                .FirstOrDefaultAsync(tc => tc.TestCategoryId == testCategoryId);
            if (category == null) return NotFound();

            var test = new Test
            {
                TestCategoryId = testCategoryId,
                TestCategory = category
            };
            return View(test);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTest(Test model, IFormFile? mediaFile)
        {
            var category = await _ctx.TestCategories
                .Include(tc => tc.Sprint)
                .FirstOrDefaultAsync(tc => tc.TestCategoryId == model.TestCategoryId);

            if (category == null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(model.Name) || string.IsNullOrWhiteSpace(model.Description))
            {
                ModelState.AddModelError("", "Name and Description are required.");
                model.TestCategory = category;
                return View(model);
            }

            model.Name = TestTitleSanitizer.Clean(model.Name);
            model.ExecutionStatus = ExecutionStatus.NotRun;
            model.ScopeStatus = model.ScopeStatus == 0 ? ScopeStatus.InScope : model.ScopeStatus;
            model.Production = model.Production ?? "";
            model.Comments = model.Comments ?? "";
            model.MediaUrl = model.MediaUrl ?? "";

            if (mediaFile != null && mediaFile.Length > 0)
            {
                var uploadResult = await SaveMediaFileAsync(mediaFile);
                if (!uploadResult.Succeeded)
                {
                    ModelState.AddModelError("", uploadResult.ErrorMessage!);
                    model.TestCategory = category;
                    return View(model);
                }

                model.MediaUrl = uploadResult.MediaUrl;
            }

            _ctx.Tests.Add(model);
            await _ctx.SaveChangesAsync();

            category = await _ctx.TestCategories
                .Include(tc => tc.Sprint)
                .FirstOrDefaultAsync(tc => tc.TestCategoryId == model.TestCategoryId);

            if (category?.Sprint?.IsTemplate == true)
            {
                await SyncTemplateTestCaseToActiveCyclesAsync(category, model);
            }

            return RedirectToAction("Details", new { id = model.TestCategoryId });
        }

        [HttpPost]
        [Authorize(Roles = AppRoles.Managers)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTest(int testId, int testCategoryId)
        {
            var test = await _ctx.Tests.FindAsync(testId);
            if (test == null) return NotFound();

            DeleteMediaFile(test.MediaUrl);
            _ctx.Tests.Remove(test);
            await _ctx.SaveChangesAsync();
            return RedirectToAction("Details", new { id = testCategoryId });
        }

        private async Task<(bool Succeeded, string? MediaUrl, string? ErrorMessage)> SaveMediaFileAsync(IFormFile mediaFile)
        {
            if (mediaFile.Length > MaxMediaFileSizeBytes)
            {
                return (false, null, "Media file is too large. Maximum allowed size is 50MB.");
            }

            var extension = Path.GetExtension(mediaFile.FileName);
            if (string.IsNullOrWhiteSpace(extension) || !AllowedMediaExtensions.Contains(extension))
            {
                return (false, null, "Unsupported file type. Allowed formats: JPG, PNG, GIF, WEBP, BMP, MP4, WEBM, MOV, AVI.");
            }

            var webRoot = _environment.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRoot))
            {
                webRoot = Path.Combine(_environment.ContentRootPath, "wwwroot");
            }

            var targetDirectory = Path.Combine(webRoot, "uploads", "test-media");
            Directory.CreateDirectory(targetDirectory);

            var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
            var filePath = Path.Combine(targetDirectory, fileName);

            await using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await mediaFile.CopyToAsync(stream);
            }

            return (true, $"/uploads/test-media/{fileName}", null);
        }

        private void DeleteMediaFile(string? mediaUrl)
        {
            if (string.IsNullOrWhiteSpace(mediaUrl) || !mediaUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var webRoot = _environment.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRoot))
            {
                webRoot = Path.Combine(_environment.ContentRootPath, "wwwroot");
            }

            var relativePath = mediaUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(webRoot, relativePath);
            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }
        }

        [HttpPost]
        [Authorize(Roles = AppRoles.Managers)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var category = await _ctx.TestCategories
                .FirstOrDefaultAsync(tc => tc.TestCategoryId == id);

            if (category == null)
            {
                TempData["ErrorMessage"] = "Test category not found.";
                return RedirectToAction("Index", "Home");
            }

            var sprintId = category.SprintId;

            _ctx.TestCategories.Remove(category);
            await _ctx.SaveChangesAsync();

            TempData["SuccessMessage"] = "Test category has been deleted.";
            return RedirectToAction("Index", "Home", new { sprintId });
        }

        [HttpGet]
        [Authorize(Roles = AppRoles.Managers)]
        public async Task<IActionResult> EditCategory(int id, string? returnUrl)
        {
            var category = await _ctx.TestCategories
                .Include(tc => tc.Sprint)
                .FirstOrDefaultAsync(tc => tc.TestCategoryId == id);

            if (category == null)
            {
                TempData["ErrorMessage"] = "Test category not found.";
                return RedirectToAction("Index", "Home");
            }

            var model = new EditCategoryInputModel
            {
                TestCategoryId = category.TestCategoryId,
                SprintId = category.SprintId,
                Name = category.Name,
                Description = category.Description,
                Department = category.Department,
                Sequence = category.Sequence,
                BuildNr = category.Sprint.BuildNr,
                ReturnUrl = string.IsNullOrWhiteSpace(returnUrl)
                    ? Url.Action("Details", new { id = category.TestCategoryId })
                    : returnUrl
            };

            return View(model);
        }

        [HttpPost]
        [Authorize(Roles = AppRoles.Managers)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCategory(EditCategoryInputModel model)
        {
            var category = await _ctx.TestCategories
                .Include(tc => tc.Sprint)
                .FirstOrDefaultAsync(tc => tc.TestCategoryId == model.TestCategoryId);

            if (category == null)
            {
                TempData["ErrorMessage"] = "Test category not found.";
                return RedirectToAction("Index", "Home");
            }

            if (!ModelState.IsValid)
            {
                model.BuildNr = category.Sprint.BuildNr;
                model.ReturnUrl ??= Url.Action("Details", new { id = category.TestCategoryId });
                return View(model);
            }

            category.Name = model.Name.Trim();
            category.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
            category.Department = model.Department;
            category.Sequence = model.Sequence;

            await _ctx.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return LocalRedirect(model.ReturnUrl);
            }

            return RedirectToAction("Details", new { id = category.TestCategoryId });
        }

        [HttpGet]
        [Authorize(Roles = AppRoles.Managers)]
        public async Task<IActionResult> CreateCategory(int sprintId, string? returnUrl)
        {
            var sprint = await _ctx.Sprints.FindAsync(sprintId);
            if (sprint == null)
            {
                TempData["ErrorMessage"] = "Select a valid version first to add a category.";
                return RedirectToAction("Index", "TestPlanVersion");
            }

            var category = new CreateCategoryInputModel
            {
                SprintId = sprintId,
                BuildNr = sprint.BuildNr,
                ReturnUrl = string.IsNullOrWhiteSpace(returnUrl)
                    ? Url.Action("Index", "Home", new { sprintId })
                    : returnUrl,
                Department = Department.IT,
                Sequence = 1
            };

            return View(category);
        }

        [HttpPost]
        [Authorize(Roles = AppRoles.Managers)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCategory(CreateCategoryInputModel model)
        {
            var sprint = await _ctx.Sprints.FindAsync(model.SprintId);
            if (sprint == null)
            {
                TempData["ErrorMessage"] = "The selected version no longer exists.";
                return RedirectToAction("Index", "TestPlanVersion");
            }

            if (!ModelState.IsValid)
            {
                model.BuildNr = sprint.BuildNr;
                model.ReturnUrl ??= Url.Action("Index", "Home", new { sprintId = model.SprintId });
                return View(model);
            }

            var category = new TestCategory
            {
                SprintId = model.SprintId,
                Name = model.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
                Department = model.Department,
                Sequence = model.Sequence,
                TotalTest = 0,
                OutOfScope = 0,
                Failed = 0,
                Blocked = 0,
                Passed = 0,
                PercentagePassed = 0,
                TestDate = null
            };

            _ctx.TestCategories.Add(category);
            await _ctx.SaveChangesAsync();

            if (sprint.IsTemplate)
            {
                await SyncTemplateCategoryToActiveCyclesAsync(sprint.SprintId, category);
            }

            if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return LocalRedirect(model.ReturnUrl);
            }

            return RedirectToAction("Index", "Home", new { sprintId = model.SprintId });
        }

        private async Task SyncTemplateCategoryToActiveCyclesAsync(int templateSprintId, TestCategory templateCategory)
        {
            var activeCycleIds = await _ctx.Sprints
                .Where(s => !s.IsArchived && !s.IsTemplate && s.SourceTemplateSprintId == templateSprintId)
                .Select(s => s.SprintId)
                .ToListAsync();

            if (activeCycleIds.Count == 0)
            {
                return;
            }

            foreach (var cycleId in activeCycleIds)
            {
                var exists = await _ctx.TestCategories.AnyAsync(tc =>
                    tc.SprintId == cycleId &&
                    tc.Name == templateCategory.Name &&
                    tc.Sequence == templateCategory.Sequence);

                if (exists)
                {
                    continue;
                }

                _ctx.TestCategories.Add(new TestCategory
                {
                    SprintId = cycleId,
                    Name = templateCategory.Name,
                    Description = templateCategory.Description,
                    Department = templateCategory.Department,
                    Sequence = templateCategory.Sequence,
                    TotalTest = 0,
                    OutOfScope = 0,
                    Failed = 0,
                    Blocked = 0,
                    Passed = 0,
                    PercentagePassed = 0,
                    TestDate = null
                });
            }

            await _ctx.SaveChangesAsync();
        }

        private async Task SyncTemplateTestCaseToActiveCyclesAsync(TestCategory templateCategory, Test templateTest)
        {
            var activeCycleIds = await _ctx.Sprints
                .Where(s => !s.IsArchived && !s.IsTemplate && s.SourceTemplateSprintId == templateCategory.SprintId)
                .Select(s => s.SprintId)
                .ToListAsync();

            if (activeCycleIds.Count == 0)
            {
                return;
            }

            foreach (var cycleId in activeCycleIds)
            {
                var targetCategory = await _ctx.TestCategories
                    .FirstOrDefaultAsync(tc =>
                        tc.SprintId == cycleId &&
                        tc.Name == templateCategory.Name &&
                        tc.Sequence == templateCategory.Sequence);

                if (targetCategory == null)
                {
                    targetCategory = new TestCategory
                    {
                        SprintId = cycleId,
                        Name = templateCategory.Name,
                        Description = templateCategory.Description,
                        Department = templateCategory.Department,
                        Sequence = templateCategory.Sequence,
                        TotalTest = 0,
                        OutOfScope = 0,
                        Failed = 0,
                        Blocked = 0,
                        Passed = 0,
                        PercentagePassed = 0,
                        TestDate = null
                    };

                    _ctx.TestCategories.Add(targetCategory);
                    await _ctx.SaveChangesAsync();
                }

                var exists = await _ctx.Tests.AnyAsync(t =>
                    t.TestCategoryId == targetCategory.TestCategoryId &&
                    t.TemplateTestCaseId == templateTest.TestId);

                if (exists)
                {
                    continue;
                }

                _ctx.Tests.Add(new Test
                {
                    TestCategoryId = targetCategory.TestCategoryId,
                    TemplateTestCaseId = templateTest.TestId,
                    IsTemplateDerived = true,
                    Name = templateTest.Name,
                    Description = templateTest.Description,
                    ScopeStatus = templateTest.ScopeStatus,
                    ExecutionStatus = ExecutionStatus.NotRun,
                    Comments = string.Empty,
                    MediaUrl = templateTest.MediaUrl,
                    Production = templateTest.Production
                });
            }

            await _ctx.SaveChangesAsync();
        }
    }
}