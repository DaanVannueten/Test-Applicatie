using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TestPlanManager.Data;
using TestPlanManager.Models;

namespace TestPlanManager.Controllers
{
    [Authorize]
    public class TestPlanVersionController : Controller
    {
        private readonly TestPlanContext _ctx;
        private readonly IDefaultVersionStore _defaultVersionStore;

        private bool IsAdmin => User.IsInRole(AppRoles.Administrator);
        private bool IsTestManager => User.IsInRole(AppRoles.TestManager);
        private bool IsTester => User.IsInRole(AppRoles.Tester);
        private bool IsManagerOrAdmin => IsAdmin || IsTestManager;
        private bool CanArchiveCycles => IsManagerOrAdmin || IsTester;

        public TestPlanVersionController(TestPlanContext ctx, IDefaultVersionStore defaultVersionStore)
        {
            _ctx = ctx;
            _defaultVersionStore = defaultVersionStore;
        }

        public async Task<IActionResult> Index()
        {
            if (!IsManagerOrAdmin && !IsTester)
            {
                return Forbid();
            }

            var defaultSprintId = _defaultVersionStore.GetDefaultSprintId();

            var versions = await BuildVersionQuery(showArchived: false).ToListAsync();

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
            if (!IsManagerOrAdmin && !IsTester)
            {
                return Forbid();
            }

            var versions = await BuildVersionQuery(showArchived: true).ToListAsync();

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
            if (!IsManagerOrAdmin)
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                // Tests expect a specific, user-friendly message when creating a new build with invalid input.
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
                BuildNr = input.BuildNr,
                IsTemplate = input.IsTemplate
            };

            _ctx.Sprints.Add(sprint);
            await _ctx.SaveChangesAsync();

            TempData["SuccessMessage"] = input.IsTemplate
                ? $"Template version {input.BuildNr} has been created."
                : $"Empty test cycle {input.BuildNr} has been created.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Copy(CopyVersionInputModel input)
        {
            if (!IsManagerOrAdmin && !IsTester)
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Where(m => !string.IsNullOrWhiteSpace(m)).ToList();
                TempData["ErrorMessage"] = errors.Any()
                    ? string.Join(" ", errors)
                    : "Use only letters, numbers, periods (.), underscores (_), or hyphens (-). Spaces are not allowed.";
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

            if (!sourceSprint.IsTemplate)
            {
                TempData["ErrorMessage"] = "New test cycles can only be created from a template version.";
                return RedirectToAction(nameof(Index));
            }

            var newSprint = new Sprint
            {
                BuildNr = input.BuildNr,
                IsTemplate = false,
                SourceTemplateSprintId = sourceSprint.SprintId
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
                        TemplateTestCaseId = sourceTest.TestId,
                        IsTemplateDerived = true,
                        Name = TestTitleSanitizer.Clean(sourceTest.Name),
                        Description = sourceTest.Description ?? string.Empty,
                        Dependencies = sourceTest.Dependencies ?? string.Empty,
                        ScopeStatus = sourceTest.ScopeStatus,
                        ExecutionStatus = ExecutionStatus.NotRun,
                        Comments = string.Empty,
                        MediaUrl = sourceTest.MediaUrl,
                        Production = string.Empty
                    }).ToList()
                };

                _ctx.TestCategories.Add(copiedCategory);
            }

            await _ctx.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Test cycle {input.BuildNr} has been created from template {sourceSprint.BuildNr}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTemplateFromCycle(CreateTemplateFromCycleInputModel input)
        {
            if (!IsManagerOrAdmin)
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Where(m => !string.IsNullOrWhiteSpace(m)).ToList();
                var errorMessage = errors.Any()
                    ? string.Join(" ", errors)
                    : "Use only letters, numbers, periods (.), underscores (_), or hyphens (-). Spaces are not allowed.";

                // Return the Index view directly with ViewData so the modal can remain open and the user can correct the input.
                ViewData["ShowMakeTemplateModal"] = true;
                ViewData["ErrorMessage"] = errorMessage;
                ViewData["CreateTemplateName"] = input.BuildNr ?? string.Empty;
                ViewData["CreateTemplateSourceCycleId"] = input.SourceCycleSprintId?.ToString() ?? string.Empty;

                var defaultSprintId = _defaultVersionStore.GetDefaultSprintId();
                var versions = await BuildVersionQuery(showArchived: false).ToListAsync();
                var vm = new TestPlanVersionPageViewModel
                {
                    Versions = versions,
                    DefaultSprintId = defaultSprintId,
                    ShowArchived = false
                };

                return View("Index", vm);
            }

            input.BuildNr = input.BuildNr.Trim();

            var buildNrExists = await _ctx.Sprints.AnyAsync(s => s.BuildNr.ToLower() == input.BuildNr.ToLower());
            if (buildNrExists)
            {
                ViewData["ShowMakeTemplateModal"] = true;
                ViewData["ErrorMessage"] = $"Build {input.BuildNr} already exists.";
                ViewData["CreateTemplateName"] = input.BuildNr ?? string.Empty;
                ViewData["CreateTemplateSourceCycleId"] = input.SourceCycleSprintId?.ToString() ?? string.Empty;

                var defaultSprintId = _defaultVersionStore.GetDefaultSprintId();
                var versions = await BuildVersionQuery(showArchived: false).ToListAsync();
                var vm = new TestPlanVersionPageViewModel
                {
                    Versions = versions,
                    DefaultSprintId = defaultSprintId,
                    ShowArchived = false
                };

                return View("Index", vm);
            }

            var sourceCycle = await _ctx.Sprints
                .Include(s => s.TestCategories)
                .ThenInclude(tc => tc.Tests)
                .FirstOrDefaultAsync(s => s.SprintId == input.SourceCycleSprintId!.Value);

            if (sourceCycle == null || sourceCycle.IsTemplate)
            {
                ViewData["ShowMakeTemplateModal"] = true;
                ViewData["ErrorMessage"] = "Select a valid source cycle (non-template).";
                ViewData["CreateTemplateName"] = input.BuildNr ?? string.Empty;
                ViewData["CreateTemplateSourceCycleId"] = input.SourceCycleSprintId?.ToString() ?? string.Empty;
                ViewData["CreateTemplateSourceCycleName"] = sourceCycle?.BuildNr ?? string.Empty;

                var defaultSprintId = _defaultVersionStore.GetDefaultSprintId();
                var versions = await BuildVersionQuery(showArchived: false).ToListAsync();
                var vm = new TestPlanVersionPageViewModel
                {
                    Versions = versions,
                    DefaultSprintId = defaultSprintId,
                    ShowArchived = false
                };

                return View("Index", vm);
            }

            var newTemplate = new Sprint
            {
                BuildNr = input.BuildNr,
                IsTemplate = true,
                SourceTemplateSprintId = null
            };

            _ctx.Sprints.Add(newTemplate);
            await _ctx.SaveChangesAsync();

            foreach (var sourceCategory in sourceCycle.TestCategories.OrderBy(tc => tc.Sequence))
            {
                var copiedCategory = new TestCategory
                {
                    SprintId = newTemplate.SprintId,
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
                        Dependencies = sourceTest.Dependencies ?? string.Empty,
                        ScopeStatus = sourceTest.ScopeStatus,
                        ExecutionStatus = ExecutionStatus.NotRun,
                        Comments = string.Empty,
                        MediaUrl = sourceTest.MediaUrl,
                        Production = sourceTest.Production,
                        IsTemplateDerived = false,
                        TemplateTestCaseId = null
                    }).ToList()
                };

                _ctx.TestCategories.Add(copiedCategory);
            }

            await _ctx.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Template {input.BuildNr} has been created from cycle {sourceCycle.BuildNr}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetDefault(int sprintId)
        {
            if (!IsManagerOrAdmin)
            {
                return Forbid();
            }

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

            if (sprint.IsTemplate)
            {
                TempData["ErrorMessage"] = "Template versions cannot be set as dashboard default.";
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
            if (!IsManagerOrAdmin)
            {
                return Forbid();
            }

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
            if (!CanArchiveCycles)
            {
                return Forbid();
            }

            var sprint = await _ctx.Sprints.FindAsync(sprintId);
            if (sprint == null)
            {
                TempData["ErrorMessage"] = "Version not found.";
                return RedirectToAction(nameof(Index));
            }

            if (sprint.IsTemplate && !IsManagerOrAdmin)
            {
                TempData["ErrorMessage"] = "Only managers can archive template versions.";
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
            if (!IsManagerOrAdmin)
            {
                return Forbid();
            }

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

        private IQueryable<TestPlanVersionDto> BuildVersionQuery(bool showArchived)
        {
            return _ctx.Sprints
                .AsNoTracking()
                .Where(s => s.IsArchived == showArchived)
                .Select(s => new TestPlanVersionDto
                {
                    SprintId = s.SprintId,
                    BuildNr = s.BuildNr,
                    IsArchived = s.IsArchived,
                    IsTemplate = s.IsTemplate,
                    SourceTemplateSprintId = s.SourceTemplateSprintId,
                    CategoryCount = s.TestCategories.Count,
                    TestCount = s.TestCategories.Sum(tc => tc.Tests.Count),
                    LastExecutionDate = s.TestCategories
                        .Where(tc => tc.TestDate.HasValue)
                        .Select(tc => tc.TestDate)
                        .OrderByDescending(d => d)
                        .FirstOrDefault(),
                    LastWorkedBy = s.TestCategories
                        .SelectMany(tc => tc.Tests)
                        .Where(t => t.ExecutionStatus != ExecutionStatus.NotRun && t.ExecutedAt.HasValue)
                        .OrderByDescending(t => t.ExecutedAt)
                        .Select(t => t.LastExecutedBy)
                        .FirstOrDefault()
                })
                .OrderByDescending(v => v.SprintId);
        }
    }
}