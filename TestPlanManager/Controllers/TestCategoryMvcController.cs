using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TestPlanManager.Data;
using TestPlanManager.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.IO;

namespace TestPlanManager.Controllers
{
    [Authorize]
    public class TestCategoryMvcController : Controller
    {
        private readonly TestPlanContext _ctx;
        private readonly IWebHostEnvironment _env;

        public TestCategoryMvcController(TestPlanContext ctx, IWebHostEnvironment env)
        {
            _ctx = ctx;
            _env = env;
        }

        public IActionResult Index()
        {
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        [Authorize(Roles = AppRoles.Managers)]
        public async Task<IActionResult> CreateCategory(int sprintId, string? returnUrl = null)
        {
            var sprint = await _ctx.Sprints
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SprintId == sprintId);

            if (sprint == null)
            {
                return NotFound();
            }

            var nextSequence = await _ctx.TestCategories
                .Where(tc => tc.SprintId == sprintId)
                .Select(tc => (int?)tc.Sequence)
                .MaxAsync() ?? 0;

            var model = new CreateCategoryInputModel
            {
                SprintId = sprint.SprintId,
                BuildNr = sprint.BuildNr,
                Sequence = nextSequence + 1,
                ReturnUrl = returnUrl
            };

            return View(model);
        }

        [HttpPost]
        [Authorize(Roles = AppRoles.Managers)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCategory(CreateCategoryInputModel model)
        {
            var sprint = await _ctx.Sprints
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SprintId == model.SprintId);

            if (sprint == null)
            {
                return NotFound();
            }

            model.BuildNr = sprint.BuildNr;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var category = new TestCategory
            {
                SprintId = model.SprintId,
                Name = model.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
                Department = model.Department,
                Sequence = model.Sequence,
                TestDate = null
            };

            _ctx.TestCategories.Add(category);
            await _ctx.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }

            return RedirectToAction("Index", "Home", new { sprintId = model.SprintId });
        }

        public async Task<IActionResult> Details(int id)
        {
            var cat = await _ctx.TestCategories
                .Include(tc => tc.Tests)
                .FirstOrDefaultAsync(tc => tc.TestCategoryId == id);
            if (cat == null) return NotFound();
            cat.Recalculate();
            return View(cat);
        }

        public async Task<IActionResult> EditTest(int id)
        {
            var test = await _ctx.Tests.FindAsync(id);
            if (test == null) return NotFound();
            return View(test);
        }

        [HttpPost]
        public async Task<IActionResult> EditTest(int TestId, int TestCategoryId, string Name, ScopeStatus ScopeStatus, ExecutionStatus ExecutionStatus, string Production, string Comments, string MediaUrl, bool removeMedia = false, IFormFile? mediaFile = null)
        {
            var test = await _ctx.Tests.FindAsync(TestId);
            if (test == null) return NotFound();

            var category = await _ctx.TestCategories
                .Include(tc => tc.Tests)
                .FirstOrDefaultAsync(tc => tc.TestCategoryId == TestCategoryId);
            if (category == null) return NotFound();

            var isTester = User.IsInRole(AppRoles.Tester);

            // Preserve existing media unless a (permitted) new upload is provided
            string? finalMediaUrl = test.MediaUrl; // keep current by default

            if (!isTester)
            {
                // non-testers may remove existing media or provide external URL
                if (removeMedia)
                {
                    if (!string.IsNullOrWhiteSpace(test.MediaUrl) && test.MediaUrl.StartsWith("/uploads/"))
                    {
                        var physical = Path.Combine(_env.WebRootPath, test.MediaUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                        if (System.IO.File.Exists(physical))
                        {
                            System.IO.File.Delete(physical);
                        }
                    }
                    finalMediaUrl = string.Empty;
                }

                if (!string.IsNullOrWhiteSpace(MediaUrl))
                {
                    finalMediaUrl = MediaUrl.Trim();
                }
            }

            if (mediaFile != null && mediaFile.Length > 0)
            {
                var uploads = Path.Combine(_env.WebRootPath, "uploads", "test-media");
                Directory.CreateDirectory(uploads);
                var ext = Path.GetExtension(mediaFile.FileName);
                var fileName = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + "_" + System.Guid.NewGuid().ToString("N") + ext;
                var physicalPath = Path.Combine(uploads, fileName);
                using (var fs = System.IO.File.Create(physicalPath))
                {
                    await mediaFile.CopyToAsync(fs);
                }
                finalMediaUrl = "/uploads/test-media/" + fileName;
            }

            if (isTester)
            {
                // Testers are only allowed to change ExecutionStatus and add media.
                // Keep name, scope, production and comments unchanged.
                TestExecutionHelper.ApplyExecution(
                    test,
                    ExecutionStatus,
                    test.Production,
                    test.Comments,
                    finalMediaUrl,
                    User?.Identity?.Name,
                    DateTime.UtcNow);
            }
            else
            {
                // Managers/Admins may edit all fields
                test.Name = Name;
                test.ScopeStatus = ScopeStatus;
                TestExecutionHelper.ApplyExecution(
                    test,
                    ExecutionStatus,
                    Production,
                    Comments,
                    finalMediaUrl,
                    User?.Identity?.Name,
                    DateTime.UtcNow);
            }

            category.TestDate = TestExecutionHelper.GetLatestExecutionDate(category.Tests);

            _ctx.Tests.Update(test);
            await _ctx.SaveChangesAsync();
            return RedirectToAction("Details", new { id = TestCategoryId });
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
        public async Task<IActionResult> CreateTest(Test model, IFormFile? mediaFile = null)
        {
            if (string.IsNullOrWhiteSpace(model.Name) || string.IsNullOrWhiteSpace(model.Description))
            {
                ModelState.AddModelError("", "Name and Description are required.");
                return View(model);
            }

            model.ExecutionStatus = ExecutionStatus.NotRun;
            model.ScopeStatus = model.ScopeStatus == 0 ? ScopeStatus.InScope : model.ScopeStatus;
            model.Production = model.Production ?? "";
            model.Comments = model.Comments ?? "";

            if (mediaFile != null && mediaFile.Length > 0)
            {
                var uploads = Path.Combine(_env.WebRootPath, "uploads", "test-media");
                Directory.CreateDirectory(uploads);
                var ext = Path.GetExtension(mediaFile.FileName);
                var fileName = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + "_" + System.Guid.NewGuid().ToString("N") + ext;
                var physicalPath = Path.Combine(uploads, fileName);
                using (var fs = System.IO.File.Create(physicalPath))
                {
                    await mediaFile.CopyToAsync(fs);
                }
                model.MediaUrl = "/uploads/test-media/" + fileName;
            }

            _ctx.Tests.Add(model);
            await _ctx.SaveChangesAsync();
            return RedirectToAction("Details", new { id = model.TestCategoryId });
        }

        [HttpPost]
        [Authorize(Roles = AppRoles.Managers)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTest(int testId, int testCategoryId)
        {
            var test = await _ctx.Tests.FindAsync(testId);
            if (test == null) return NotFound();

            _ctx.Tests.Remove(test);
            await _ctx.SaveChangesAsync();
            return RedirectToAction("Details", new { id = testCategoryId });
        }
    }
}