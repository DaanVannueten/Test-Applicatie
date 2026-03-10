using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TestPlanManager.Data;
using TestPlanManager.Models;

namespace TestPlanManager.Controllers
{
    [Authorize(Roles = AppRoles.Admin)]
    public class TestPlanVersionController : Controller
    {
        private readonly TestPlanContext _ctx;
        private readonly IDefaultVersionStore _defaultVersionStore;

        public TestPlanVersionController(TestPlanContext ctx, IDefaultVersionStore defaultVersionStore)
        {
            _ctx = ctx;
            _defaultVersionStore = defaultVersionStore;
        }

        public async Task<IActionResult> Index()
        {
            var defaultSprintId = _defaultVersionStore.GetDefaultSprintId();

            var versions = await _ctx.Sprints
                .Where(s => !s.IsArchived)
                .Include(s => s.TestCategories)
                .ThenInclude(tc => tc.Tests)
                .Select(s => new TestPlanVersionDto
                {
                    SprintId = s.SprintId,
                    BuildNr = s.BuildNr,
                    IsArchived = s.IsArchived,
                    CategoryCount = s.TestCategories.Count,
                    TestCount = s.TestCategories.Sum(tc => tc.Tests.Count),
                    LastExecutionDate = s.TestCategories
                        .Where(tc => tc.TestDate.HasValue)
                        .Select(tc => tc.TestDate)
                        .OrderByDescending(d => d)
                        .FirstOrDefault()
                })
                .OrderByDescending(v => v.SprintId)
                .ToListAsync();

            if (defaultSprintId.HasValue)
            {
                versions = versions
                    .OrderBy(v => v.SprintId == defaultSprintId.Value ? 0 : 1)
                    .ThenByDescending(v => v.SprintId)
                    .ToList();
            }

            var vm = new TestPlanVersionPageViewModel
            {
                Versions = versions,
                DefaultSprintId = defaultSprintId,
                ShowArchived = false
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Archived()
        {
            var versions = await _ctx.Sprints
                .Where(s => s.IsArchived)
                .Include(s => s.TestCategories)
                .ThenInclude(tc => tc.Tests)
                .Select(s => new TestPlanVersionDto
                {
                    SprintId = s.SprintId,
                    BuildNr = s.BuildNr,
                    IsArchived = s.IsArchived,
                    CategoryCount = s.TestCategories.Count,
                    TestCount = s.TestCategories.Sum(tc => tc.Tests.Count),
                    LastExecutionDate = s.TestCategories
                        .Where(tc => tc.TestDate.HasValue)
                        .Select(tc => tc.TestDate)
                        .OrderByDescending(d => d)
                        .FirstOrDefault()
                })
                .OrderByDescending(v => v.SprintId)
                .ToListAsync();

            var vm = new TestPlanVersionPageViewModel
            {
                Versions = versions,
                DefaultSprintId = _defaultVersionStore.GetDefaultSprintId(),
                ShowArchived = true
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateVersionInputModel input)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "New build number is invalid.";
                return RedirectToAction(nameof(Index));
            }

            input.BuildNr = input.BuildNr.Trim();

            var buildNrExists = await _ctx.Sprints.AnyAsync(s => s.BuildNr.ToLower() == input.BuildNr.ToLower());
            if (buildNrExists)
            {
                TempData["ErrorMessage"] = $"Build {input.BuildNr} already exists.";
                return RedirectToAction(nameof(Index));
            }

            var sprint = new Sprint
            {
                BuildNr = input.BuildNr
            };

            _ctx.Sprints.Add(sprint);
            await _ctx.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Empty build {input.BuildNr} has been created.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Copy(CopyVersionInputModel input)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Enter a valid build number and source version.";
                return RedirectToAction(nameof(Index));
            }

            input.BuildNr = input.BuildNr.Trim();

            var buildNrExists = await _ctx.Sprints.AnyAsync(s => s.BuildNr.ToLower() == input.BuildNr.ToLower());
            if (buildNrExists)
            {
                TempData["ErrorMessage"] = $"Build {input.BuildNr} already exists.";
                return RedirectToAction(nameof(Index));
            }

            var sourceSprint = await _ctx.Sprints
                .Include(s => s.TestCategories)
                .ThenInclude(tc => tc.Tests)
                .FirstOrDefaultAsync(s => s.SprintId == input.CopyFromSprintId!.Value);

            if (sourceSprint == null)
            {
                TempData["ErrorMessage"] = "The source version to copy does not exist.";
                return RedirectToAction(nameof(Index));
            }

            var newSprint = new Sprint
            {
                BuildNr = input.BuildNr
            };

            _ctx.Sprints.Add(newSprint);
            await _ctx.SaveChangesAsync();

            foreach (var sourceCategory in sourceSprint.TestCategories.OrderBy(tc => tc.Sequence))
            {
                var copiedCategory = new TestCategory
                {
                    SprintId = newSprint.SprintId,
                    Name = sourceCategory.Name,
                    Description = sourceCategory.Description,
                    Sequence = sourceCategory.Sequence,
                    Department = sourceCategory.Department,
                    TestDate = null,
                    TotalTest = 0,
                    OutOfScope = 0,
                    Failed = 0,
                    Blocked = 0,
                    Passed = 0,
                    PercentagePassed = 0,
                    Tests = sourceCategory.Tests.Select(sourceTest => new Test
                    {
                        Name = TestTitleSanitizer.Clean(sourceTest.Name),
                        Description = sourceTest.Description ?? string.Empty,
                        ScopeStatus = sourceTest.ScopeStatus,
                        ExecutionStatus = ExecutionStatus.NotRun,
                        Comments = string.Empty,
                        MediaUrl = string.Empty,
                        Production = string.Empty
                    }).ToList()
                };

                _ctx.TestCategories.Add(copiedCategory);
            }

            await _ctx.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Build {input.BuildNr} has been created by copying build {sourceSprint.BuildNr}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetDefault(int sprintId)
        {
            var sprint = await _ctx.Sprints.FirstOrDefaultAsync(s => s.SprintId == sprintId);
            if (sprint == null)
            {
                TempData["ErrorMessage"] = "Version not found.";
                return RedirectToAction(nameof(Index));
            }

            if (sprint.IsArchived)
            {
                TempData["ErrorMessage"] = "Archived versions cannot be set as default.";
                return RedirectToAction(nameof(Index));
            }

            _defaultVersionStore.SetDefaultSprintId(sprintId);
            TempData["SuccessMessage"] = "Default version has been updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int sprintId)
        {
            var sprint = await _ctx.Sprints.FindAsync(sprintId);
            if (sprint == null)
            {
                TempData["ErrorMessage"] = "Version not found.";
                return RedirectToAction(nameof(Index));
            }

            _ctx.Sprints.Remove(sprint);
            await _ctx.SaveChangesAsync();

            var currentDefaultSprintId = _defaultVersionStore.GetDefaultSprintId();
            if (currentDefaultSprintId == sprintId)
            {
                var fallbackSprintId = await _ctx.Sprints
                    .Where(s => !s.IsArchived)
                    .OrderByDescending(s => s.SprintId)
                    .Select(s => (int?)s.SprintId)
                    .FirstOrDefaultAsync();

                _defaultVersionStore.SetDefaultSprintId(fallbackSprintId);
            }

            TempData["SuccessMessage"] = $"Build {sprint.BuildNr} has been deleted.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Archive(int sprintId)
        {
            var sprint = await _ctx.Sprints.FindAsync(sprintId);
            if (sprint == null)
            {
                TempData["ErrorMessage"] = "Version not found.";
                return RedirectToAction(nameof(Index));
            }

            sprint.IsArchived = true;
            await _ctx.SaveChangesAsync();

            var currentDefaultSprintId = _defaultVersionStore.GetDefaultSprintId();
            if (currentDefaultSprintId == sprintId)
            {
                var fallbackSprintId = await _ctx.Sprints
                    .Where(s => !s.IsArchived)
                    .OrderByDescending(s => s.SprintId)
                    .Select(s => (int?)s.SprintId)
                    .FirstOrDefaultAsync();

                _defaultVersionStore.SetDefaultSprintId(fallbackSprintId);
            }

            TempData["SuccessMessage"] = $"Build {sprint.BuildNr} has been archived.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int sprintId)
        {
            var sprint = await _ctx.Sprints.FindAsync(sprintId);
            if (sprint == null)
            {
                TempData["ErrorMessage"] = "Version not found.";
                return RedirectToAction(nameof(Archived));
            }

            sprint.IsArchived = false;
            await _ctx.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Build {sprint.BuildNr} has been restored.";
            return RedirectToAction(nameof(Archived));
        }
    }
}