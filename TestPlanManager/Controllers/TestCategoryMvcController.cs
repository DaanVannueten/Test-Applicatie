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
            var test = await _ctx.Tests.FindAsync(id);
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
            }
            else
            {
                test.ExecutedAt = null;
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
            var category = await _ctx.TestCategories.FindAsync(testCategoryId);
            if (category == null) return NotFound();

            var test = new Test { TestCategoryId = testCategoryId };
            return View(test);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTest(Test model, IFormFile? mediaFile)
        {
            if (string.IsNullOrWhiteSpace(model.Name) || string.IsNullOrWhiteSpace(model.Description))
            {
                ModelState.AddModelError("", "Name and Description are required.");
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
                    return View(model);
                }

                model.MediaUrl = uploadResult.MediaUrl;
            }

            _ctx.Tests.Add(model);
            await _ctx.SaveChangesAsync();
            return RedirectToAction("Details", new { id = model.TestCategoryId });
        }

        [HttpPost]
        [Authorize(Roles = AppRoles.Admin)]
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
        [Authorize(Roles = AppRoles.Admin)]
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
        [Authorize(Roles = AppRoles.Admin)]
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
        [Authorize(Roles = AppRoles.Admin)]
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
        [Authorize(Roles = AppRoles.Admin)]
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
        [Authorize(Roles = AppRoles.Admin)]
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

            if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return LocalRedirect(model.ReturnUrl);
            }

            return RedirectToAction("Index", "Home", new { sprintId = model.SprintId });
        }
    }
}