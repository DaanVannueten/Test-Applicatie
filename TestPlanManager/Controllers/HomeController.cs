using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TestPlanManager.Models;

namespace TestPlanManager.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly TestPlanManager.Data.TestPlanContext _ctx;
    private readonly TestPlanManager.Data.IDefaultVersionStore _defaultVersionStore;
    public HomeController(
        TestPlanManager.Data.TestPlanContext ctx,
        TestPlanManager.Data.IDefaultVersionStore defaultVersionStore)
    {
        _ctx = ctx;
        _defaultVersionStore = defaultVersionStore;
    }

    [HttpGet]
    public IActionResult GoToDefault()
    {
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Index(int? sprintId, bool includeTemplates = false)
    {
        if (!sprintId.HasValue && !includeTemplates)
        {
            return RedirectToAction("Index", "TestPlanVersion");
        }

        var canViewTemplates = includeTemplates && (User.IsInRole(AppRoles.Administrator) || User.IsInRole(AppRoles.TestManager));

        var sprints = await _ctx.Sprints
            .AsNoTracking()
            .Where(s => !s.IsArchived && (!s.IsTemplate || canViewTemplates))
            .Include(s => s.TestCategories)
            .ThenInclude(tc => tc.Tests)
            .OrderByDescending(s => s.SprintId)
            .ToListAsync();

        var versionDtos = sprints.Select(s => new TestPlanVersionDto
        {
            SprintId = s.SprintId,
            BuildNr = s.BuildNr,
            IsArchived = s.IsArchived,
            IsTemplate = s.IsTemplate,
            CategoryCount = s.TestCategories.Count,
            TestCount = s.TestCategories.Sum(tc => tc.Tests.Count),
            LastExecutionDate = s.TestCategories
                .Where(tc => tc.TestDate.HasValue)
                .Select(tc => tc.TestDate)
                .OrderByDescending(d => d)
                .FirstOrDefault()
        }).ToList();

        var defaultSprintId = _defaultVersionStore.GetDefaultSprintId();

        var selectedSprint = sprintId.HasValue
            ? sprints.FirstOrDefault(s => s.SprintId == sprintId.Value)
            : defaultSprintId.HasValue
                ? sprints.FirstOrDefault(s => s.SprintId == defaultSprintId.Value)
                : null;

        selectedSprint ??= sprints.FirstOrDefault();

        var categories = selectedSprint?.TestCategories.ToList() ?? new List<TestCategory>();

        // recalc the aggregates on each category entity
        foreach (var cat in categories)
        {
            cat.Recalculate();
        }

        // metric calculations based on the actual test rows
        var tests = categories.SelectMany(tc => tc.Tests).ToList();
        var totalTestCases = tests.Count;

        var testsExecuted = tests.Count(t => t.ExecutionStatus != Models.ExecutionStatus.NotRun);

        var inScopeTests = tests.Where(t => t.ScopeStatus == Models.ScopeStatus.InScope).ToList();
        var inScopeTotal = inScopeTests.Count;

        var passed = tests.Count(t => t.ExecutionStatus == Models.ExecutionStatus.Passed);
        var failed = tests.Count(t => t.ExecutionStatus == Models.ExecutionStatus.Failed);
        var blocked = tests.Count(t => t.ExecutionStatus == Models.ExecutionStatus.Blocked);
        var outOfScope = tests.Count(t => t.ScopeStatus == Models.ScopeStatus.OutOfScope);
        var openOrFailed = tests.Count(t => t.ExecutionStatus == Models.ExecutionStatus.Failed ||
                                           t.ExecutionStatus == Models.ExecutionStatus.Blocked);

        var passedInScope = inScopeTests.Count(t => t.ExecutionStatus == Models.ExecutionStatus.Passed);

        var overallPassRate = inScopeTotal == 0 ? 0f : ((float)passedInScope / inScopeTotal) * 100;
        var passedRate = inScopeTotal == 0 ? 0f : ((float)passedInScope / inScopeTotal) * 100;
        var failedRate = totalTestCases == 0 ? 0f : ((float)failed / totalTestCases) * 100;
        var blockedRate = totalTestCases == 0 ? 0f : ((float)blocked / totalTestCases) * 100;
        var outOfScopeRate = totalTestCases == 0 ? 0f : ((float)outOfScope / totalTestCases) * 100;

        var dtos = categories.Select(tc => new Models.TestCategoryDto
        {
            TestCategoryId = tc.TestCategoryId,
            Name = tc.Name,
            Description = tc.Description,
            BuildNr = selectedSprint?.BuildNr ?? string.Empty,
            Department = tc.Department,

            Passed = tc.Passed,
            Failed = tc.Failed,
            Blocked = tc.Blocked,
            OutOfScope = tc.OutOfScope,
            TotalTest = tc.TotalTest,
            PercentagePassed = tc.PercentagePassed
        }).ToList();

        var vm = new Models.DashboardViewModel
        {
            Versions = versionDtos,
            SelectedSprintId = selectedSprint?.SprintId,
            SelectedBuildNr = selectedSprint?.BuildNr,
            SelectedIsTemplate = selectedSprint?.IsTemplate ?? false,
            IncludeTemplatesView = canViewTemplates,
            Categories = dtos,
            TotalTestCases = totalTestCases,
            TestsExecuted = testsExecuted,
            OverallPassRate = overallPassRate,
            OpenOrFailed = openOrFailed,
            PassedRate = passedRate,
            FailedRate = failedRate,
            BlockedRate = blockedRate,
            OutOfScopeRate = outOfScopeRate
        };

        return View(vm);
    }

    [HttpPost]
    [Authorize(Roles = AppRoles.Managers)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetAllTests(int? sprintId)
    {
        var targetSprintId = sprintId ?? await _ctx.Sprints
            .Where(s => !s.IsArchived && !s.IsTemplate)
            .OrderByDescending(s => s.SprintId)
            .Select(s => (int?)s.SprintId)
            .FirstOrDefaultAsync();

        if (!targetSprintId.HasValue)
        {
            return RedirectToAction(nameof(Index));
        }

        var allCategories = await _ctx.TestCategories
            .Include(tc => tc.Tests)
            .Where(tc => tc.SprintId == targetSprintId.Value)
            .ToListAsync();

        foreach (var category in allCategories)
        {
            foreach (var test in category.Tests)
            {
                test.ExecutionStatus = Models.ExecutionStatus.NotRun;
                test.ExecutedAt = null;
                test.LastExecutedBy = null;
            }

            category.TestDate = null;
        }

        await _ctx.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { sprintId = targetSprintId.Value });
    }

    [HttpPost]
    [Authorize(Roles = $"{AppRoles.Managers},{AppRoles.Tester}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ArchiveCycle(int sprintId)
    {
        var sprint = await _ctx.Sprints.FirstOrDefaultAsync(s => s.SprintId == sprintId);
        if (sprint == null)
        {
            TempData["ErrorMessage"] = "Test cycle not found.";
            return RedirectToAction(nameof(Index));
        }

        if (sprint.IsTemplate)
        {
            TempData["ErrorMessage"] = "Template versions cannot be archived from dashboard.";
            return RedirectToAction(nameof(Index));
        }

        sprint.IsArchived = true;
        await _ctx.SaveChangesAsync();

        var currentDefaultSprintId = _defaultVersionStore.GetDefaultSprintId();
        if (currentDefaultSprintId == sprintId)
        {
            var fallbackSprintId = await _ctx.Sprints
                .Where(s => !s.IsArchived && !s.IsTemplate)
                .OrderByDescending(s => s.SprintId)
                .Select(s => (int?)s.SprintId)
                .FirstOrDefaultAsync();

            _defaultVersionStore.SetDefaultSprintId(fallbackSprintId);
        }

        TempData["SuccessMessage"] = $"Test cycle Build {sprint.BuildNr} archived.";
        return RedirectToAction(nameof(Index));
    }
}
