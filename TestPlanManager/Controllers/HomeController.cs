using System.Diagnostics;
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
        var categories = await _ctx.TestCategories
            .Include(tc => tc.Sprint)
            .Select(tc => new Models.TestCategoryDto
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
            })
            .ToListAsync();

        return View(new Models.DashboardViewModel { Categories = categories });
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
