using System.Diagnostics;
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

    public async Task<IActionResult> Index(int? sprintId)
    {
        var sprints = await _ctx.Sprints
            .Where(s => !s.IsArchived)
            .Include(s => s.TestCategories)
            .ThenInclude(tc => tc.Tests)
            .OrderByDescending(s => s.SprintId)
            .ToListAsync();

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

        var todayUtc = DateTime.UtcNow.Date;
        var testsExecuted = tests.Count(t =>
            t.ExecutionStatus != Models.ExecutionStatus.NotRun &&
            t.ExecutedAt.HasValue &&
            t.ExecutedAt.Value.Date == todayUtc);

        var passed = tests.Count(t => t.ExecutionStatus == Models.ExecutionStatus.Passed);
        var openOrFailed = tests.Count(t => t.ExecutionStatus == Models.ExecutionStatus.Failed ||
                                           t.ExecutionStatus == Models.ExecutionStatus.Blocked);

        var overallPassRate = totalTestCases == 0 ? 0f : ((float)passed / totalTestCases) * 100;

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
            SelectedSprintId = selectedSprint?.SprintId,
            SelectedBuildNr = selectedSprint?.BuildNr,
            Categories = dtos,
            TotalTestCases = totalTestCases,
            TestsExecutedToday = testsExecuted,
            OverallPassRate = overallPassRate,
            OpenOrFailed = openOrFailed
        };

        return View(vm);
    }

    [HttpPost]
    [Authorize(Roles = AppRoles.Admin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetAllTests(int? sprintId)
    {
        var targetSprintId = sprintId ?? await _ctx.Sprints
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
            }

            category.TestDate = null;
        }

        await _ctx.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { sprintId = targetSprintId.Value });
    }
}
