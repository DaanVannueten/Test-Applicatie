using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TestPlanManager.Data;
using TestPlanManager.Models;

namespace TestPlanManager.Controllers
{
    public class TestCategoryMvcController : Controller
    {
        private readonly TestPlanContext _ctx;
        private readonly IWebHostEnvironment _env;
        private const long MaxMediaFileBytes = 50 * 1024 * 1024;
        private static readonly HashSet<string> AllowedMediaExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".mp4", ".webm", ".mov", ".avi"
        };

        public TestCategoryMvcController(TestPlanContext ctx, IWebHostEnvironment env)
        {
            _ctx = ctx;
            _env = env;
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
        public async Task<IActionResult> EditTest(int TestId, int TestCategoryId, string Name, ScopeStatus ScopeStatus, ExecutionStatus ExecutionStatus, string Production, string Comments, string? MediaUrl, IFormFile? mediaFile, bool removeMedia = false)
        {
            var test = await _ctx.Tests.FindAsync(TestId);
            if (test == null) return NotFound();

            var previousStatus = test.ExecutionStatus;

            var category = await _ctx.TestCategories
                .Include(tc => tc.Tests)
                .FirstOrDefaultAsync(tc => tc.TestCategoryId == TestCategoryId);
            if (category == null) return NotFound();

            test.Name = TestTitleSanitizer.Clean(Name);
            test.ScopeStatus = ScopeStatus;
            test.ExecutionStatus = ExecutionStatus;
            test.Production = Production ?? "";
            test.Comments = Comments ?? "";

            var previousMediaUrl = test.MediaUrl;

            if (removeMedia)
            {
                test.MediaUrl = string.Empty;
            }

            if (mediaFile is not null && mediaFile.Length > 0)
            {
                if (!TryValidateMediaFile(mediaFile, out var validationError))
                {
                    ModelState.AddModelError("mediaFile", validationError);
                    return View(test);
                }

                var newMediaUrl = await SaveMediaFileAsync(mediaFile);
                test.MediaUrl = newMediaUrl;
            }
            else if (!removeMedia && !string.IsNullOrWhiteSpace(MediaUrl))
            {
                test.MediaUrl = MediaUrl.Trim();
            }

            if (ExecutionStatus != Models.ExecutionStatus.NotRun)
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

            if ((removeMedia || (mediaFile is not null && mediaFile.Length > 0))
                && !string.Equals(previousMediaUrl, test.MediaUrl, StringComparison.OrdinalIgnoreCase))
            {
                await DeleteMediaFileIfLocalIfUnreferencedAsync(previousMediaUrl, test.TestId);
            }

            var detailsUrl = Url.Action("Details", new { id = TestCategoryId });
            return Redirect($"{detailsUrl}#test-{TestId}");
        }

        [HttpGet]
        [Authorize(Roles = AppRoles.Managers)]
        public async Task<IActionResult> CreateTest(int testCategoryId)
        {
            var category = await _ctx.TestCategories.FindAsync(testCategoryId);
            if (category == null) return NotFound();

            var test = new Test { TestCategoryId = testCategoryId };
            return View(test);
        }

        [HttpPost]
        [Authorize(Roles = AppRoles.Managers)]
        public async Task<IActionResult> CreateTest(Test model, IFormFile? mediaFile)
        {
            if (string.IsNullOrWhiteSpace(model.Name) || string.IsNullOrWhiteSpace(model.Description))
            {
                ModelState.AddModelError("", "Name and Description are required.");
                return View(model);
            }

            if (mediaFile is not null && mediaFile.Length > 0)
            {
                if (!TryValidateMediaFile(mediaFile, out var validationError))
                {
                    ModelState.AddModelError("mediaFile", validationError);
                    return View(model);
                }

                model.MediaUrl = await SaveMediaFileAsync(mediaFile);
            }

            model.Name = TestTitleSanitizer.Clean(model.Name);
            model.ExecutionStatus = ExecutionStatus.NotRun;
            model.ScopeStatus = model.ScopeStatus == 0 ? ScopeStatus.InScope : model.ScopeStatus;
            model.Production = model.Production ?? "";
            model.Comments = model.Comments ?? "";
            model.MediaUrl = model.MediaUrl ?? "";

            _ctx.Tests.Add(model);
            await _ctx.SaveChangesAsync();
            return RedirectToAction("Details", new { id = model.TestCategoryId });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteTest(int testId, int testCategoryId)
        {
            var test = await _ctx.Tests.FindAsync(testId);
            if (test == null) return NotFound();

            _ctx.Tests.Remove(test);
            await _ctx.SaveChangesAsync();
            return RedirectToAction("Details", new { id = testCategoryId });
        }

        [HttpPost]
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

        private bool TryValidateMediaFile(IFormFile mediaFile, out string validationError)
        {
            var extension = Path.GetExtension(mediaFile.FileName);
            if (string.IsNullOrWhiteSpace(extension) || !AllowedMediaExtensions.Contains(extension))
            {
                validationError = "Unsupported file type. Allowed: JPG, PNG, GIF, WEBP, BMP, MP4, WEBM, MOV, AVI.";
                return false;
            }

            if (mediaFile.Length > MaxMediaFileBytes)
            {
                validationError = "File is too large. Maximum allowed size is 50MB.";
                return false;
            }

            validationError = string.Empty;
            return true;
        }

        private async Task<string> SaveMediaFileAsync(IFormFile mediaFile)
        {
            var extension = Path.GetExtension(mediaFile.FileName).ToLowerInvariant();
            var uniqueFileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{extension}";

            var webRoot = string.IsNullOrWhiteSpace(_env.WebRootPath)
                ? Path.Combine(_env.ContentRootPath, "wwwroot")
                : _env.WebRootPath;

            var mediaFolder = Path.Combine(webRoot, "uploads", "test-media");
            Directory.CreateDirectory(mediaFolder);

            var physicalPath = Path.Combine(mediaFolder, uniqueFileName);
            await using var stream = System.IO.File.Create(physicalPath);
            await mediaFile.CopyToAsync(stream);

            return $"/uploads/test-media/{uniqueFileName}";
        }

        private async Task DeleteMediaFileIfLocalIfUnreferencedAsync(string? mediaUrl, int excludeTestId)
        {
            if (string.IsNullOrWhiteSpace(mediaUrl) || !mediaUrl.StartsWith("/uploads/test-media/", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var stillReferenced = await _ctx.Tests
                .AsNoTracking()
                .AnyAsync(t => t.TestId != excludeTestId && t.MediaUrl == mediaUrl);

            if (stillReferenced)
            {
                return;
            }

            var fileName = Path.GetFileName(mediaUrl);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return;
            }

            var webRoot = string.IsNullOrWhiteSpace(_env.WebRootPath)
                ? Path.Combine(_env.ContentRootPath, "wwwroot")
                : _env.WebRootPath;

            var physicalPath = Path.Combine(webRoot, "uploads", "test-media", fileName);
            if (System.IO.File.Exists(physicalPath))
            {
                System.IO.File.Delete(physicalPath);
            }
        }
    }
}