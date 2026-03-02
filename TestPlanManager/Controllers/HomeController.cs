using System.Diagnostics;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TestPlanManager.Models;

namespace TestPlanManager.Controllers;

public class HomeController : Controller
{
    private readonly TestPlanManager.Data.TestPlanContext _ctx;
    public HomeController(TestPlanManager.Data.TestPlanContext ctx)
    {
        _ctx = ctx;
    }

    public async Task<IActionResult> Index()
    {
        // load categories and tests so we can do accurate computations
        var categories = await _ctx.TestCategories
            .Include(tc => tc.Sprint)
            .Include(tc => tc.Tests)
            .ToListAsync();

        // recalc the aggregates on each category entity
        foreach (var cat in categories)
        {
            cat.Recalculate();
        }

        // metric calculations based on the actual test rows
        var tests = categories.SelectMany(tc => tc.Tests.Select(t => new { t, tc.TestDate })).ToList();
        var totalTestCases = tests.Count;

        var today = DateTime.UtcNow.Date;
        var testsExecutedToday = categories
            .Where(tc => tc.TestDate.HasValue && tc.TestDate.Value.Date == today)
            .Sum(tc => tc.Tests.Count(t => t.ExecutionStatus != Models.ExecutionStatus.NotRun));

        var passed = tests.Count(x => x.t.ExecutionStatus == Models.ExecutionStatus.Passed);
        var openOrFailed = tests.Count(x => x.t.ExecutionStatus == Models.ExecutionStatus.Failed ||
                                           x.t.ExecutionStatus == Models.ExecutionStatus.Blocked);

        var overallPassRate = totalTestCases == 0 ? 0f : ((float)passed / totalTestCases) * 100;

        var dtos = categories.Select(tc => new Models.TestCategoryDto
        {
            TestCategoryId = tc.TestCategoryId,
            Name = tc.Name,
            Description = tc.Description,
            BuildNr = tc.Sprint.BuildNr,
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
            Categories = dtos,
            TotalTestCases = totalTestCases,
            TestsExecutedToday = testsExecutedToday,
            OverallPassRate = overallPassRate,
            OpenOrFailed = openOrFailed
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetAllTests()
    {
        // set every test to NotRun and clear date
        var allTests = await _ctx.Tests.ToListAsync();
        foreach (var t in allTests)
        {
            t.ExecutionStatus = Models.ExecutionStatus.NotRun;
        }

        var allCategories = await _ctx.TestCategories.ToListAsync();
        foreach (var category in allCategories)
        {
            category.TestDate = null;
        }

        await _ctx.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
