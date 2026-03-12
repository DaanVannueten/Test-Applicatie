using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TestPlanManager.Data;
using TestPlanManager.Models;

namespace TestPlanManager.Controllers;

[Authorize]
public class TemplateController : Controller
{
    private readonly TestPlanContext _ctx;

    public TemplateController(TestPlanContext ctx)
    {
        _ctx = ctx;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var templates = await _ctx.TestTemplates
            .Include(t => t.TestCases)
            .Include(t => t.Sprints)
            .OrderBy(t => t.ProductName)
            .ThenBy(t => t.Name)
            .Select(t => new TemplateOverviewViewModel
            {
                TestTemplateId = t.TestTemplateId,
                Name = t.Name,
                ProductName = t.ProductName,
                TestCaseCount = t.TestCases.Count,
                ActiveCycleCount = t.Sprints.Count(s => !s.IsArchived)
            })
            .ToListAsync();

        return View(new TemplateIndexPageViewModel { Templates = templates });
    }

    [HttpPost]
    [Authorize(Roles = AppRoles.Managers)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateTemplateInputModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Template name and product are required.";
            return RedirectToAction(nameof(Index));
        }

        var exists = await _ctx.TestTemplates.AnyAsync(t =>
            t.Name.ToLower() == model.Name.Trim().ToLower() &&
            t.ProductName.ToLower() == model.ProductName.Trim().ToLower());

        if (exists)
        {
            TempData["ErrorMessage"] = "This template already exists for the selected product.";
            return RedirectToAction(nameof(Index));
        }

        var template = new TestTemplate
        {
            Name = model.Name.Trim(),
            ProductName = model.ProductName.Trim(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        _ctx.TestTemplates.Add(template);
        await _ctx.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Template {template.Name} created.";
        return RedirectToAction(nameof(Details), new { id = template.TestTemplateId });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var template = await _ctx.TestTemplates
            .FirstOrDefaultAsync(t => t.TestTemplateId == id);
        if (template == null)
        {
            return NotFound();
        }

        var testCases = await _ctx.TemplateTestCases
            .Where(tc => tc.TestTemplateId == id)
            .OrderBy(tc => tc.Sequence)
            .ThenBy(tc => tc.TemplateTestCaseId)
            .ToListAsync();

        var vm = new TemplateDetailsPageViewModel
        {
            Template = template,
            TestCases = testCases,
            AddTestCase = new AddTemplateTestCaseInputModel { TestTemplateId = id },
            CreateCycle = new CreateCycleFromTemplateInputModel { TestTemplateId = id }
        };

        return View(vm);
    }

    [HttpPost]
    [Authorize(Roles = AppRoles.Managers)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTestCase(AddTemplateTestCaseInputModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Test case name and description are required.";
            return RedirectToAction(nameof(Details), new { id = model.TestTemplateId });
        }

        var template = await _ctx.TestTemplates
            .FirstOrDefaultAsync(t => t.TestTemplateId == model.TestTemplateId);
        if (template == null)
        {
            return NotFound();
        }

        var maxSequence = await _ctx.TemplateTestCases
            .Where(tc => tc.TestTemplateId == model.TestTemplateId)
            .Select(tc => (int?)tc.Sequence)
            .MaxAsync() ?? 0;

        var templateTestCase = new TemplateTestCase
        {
            TestTemplateId = model.TestTemplateId,
            Name = TestTitleSanitizer.Clean(model.Name),
            Description = model.Description,
            MediaUrl = string.IsNullOrWhiteSpace(model.MediaUrl) ? null : model.MediaUrl.Trim(),
            ScopeStatus = model.ScopeStatus,
            DefaultExecutionStatus = model.DefaultExecutionStatus,
            Production = model.Production ?? string.Empty,
            Sequence = maxSequence + 1
        };

        _ctx.TemplateTestCases.Add(templateTestCase);
        await _ctx.SaveChangesAsync();

        await ApplyTemplateTestCaseToActiveCyclesAsync(templateTestCase);

        TempData["SuccessMessage"] = "Template testcase toegevoegd en automatisch toegepast op actieve testcycli.";
        return RedirectToAction(nameof(Details), new { id = model.TestTemplateId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCycle(CreateCycleFromTemplateInputModel model)
    {
        model.BuildNr = model.BuildNr?.Trim() ?? string.Empty;

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Version/build number is required.";
            return RedirectToAction(nameof(Details), new { id = model.TestTemplateId });
        }

        var template = await _ctx.TestTemplates
            .Include(t => t.TestCases)
            .FirstOrDefaultAsync(t => t.TestTemplateId == model.TestTemplateId && t.IsActive);

        if (template == null)
        {
            TempData["ErrorMessage"] = "Template not found or inactive.";
            return RedirectToAction(nameof(Index));
        }

        var buildNrExists = await _ctx.Sprints.AnyAsync(s => s.BuildNr.ToLower() == model.BuildNr.ToLower());
        if (buildNrExists)
        {
            TempData["ErrorMessage"] = $"Build {model.BuildNr} already exists.";
            return RedirectToAction(nameof(Details), new { id = model.TestTemplateId });
        }

        var sprint = new Sprint
        {
            BuildNr = model.BuildNr,
            TestTemplateId = template.TestTemplateId,
            IsArchived = false
        };

        _ctx.Sprints.Add(sprint);
        await _ctx.SaveChangesAsync();

        var category = new TestCategory
        {
            SprintId = sprint.SprintId,
            TestTemplateId = template.TestTemplateId,
            IsTemplateCategory = true,
            Name = $"{template.ProductName} - {template.Name}",
            Description = $"Auto-generated from template {template.Name}",
            Department = Department.IT,
            Sequence = 1,
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

        var tests = template.TestCases
            .OrderBy(tc => tc.Sequence)
            .Select(tc => new Test
            {
                TestCategoryId = category.TestCategoryId,
                TemplateTestCaseId = tc.TemplateTestCaseId,
                IsTemplateDerived = true,
                Name = tc.Name,
                Description = tc.Description,
                MediaUrl = tc.MediaUrl,
                ScopeStatus = tc.ScopeStatus,
                ExecutionStatus = tc.DefaultExecutionStatus,
                Production = tc.Production,
                Comments = null
            })
            .ToList();

        if (tests.Count > 0)
        {
            _ctx.Tests.AddRange(tests);
            await _ctx.SaveChangesAsync();
        }

        TempData["SuccessMessage"] = $"Test cycle Build {sprint.BuildNr} created from template {template.Name}.";
        return RedirectToAction("Index", "Home", new { sprintId = sprint.SprintId });
    }

    private async Task ApplyTemplateTestCaseToActiveCyclesAsync(TemplateTestCase templateTestCase)
    {
        var targetCategories = await _ctx.TestCategories
            .Include(tc => tc.Sprint)
            .Where(tc =>
                tc.IsTemplateCategory &&
                tc.TestTemplateId == templateTestCase.TestTemplateId &&
                !tc.Sprint.IsArchived)
            .ToListAsync();

        if (targetCategories.Count == 0)
        {
            return;
        }

        var categoryIds = targetCategories.Select(tc => tc.TestCategoryId).ToList();
        var alreadyAppliedCategoryIds = await _ctx.Tests
            .Where(t =>
                t.TemplateTestCaseId == templateTestCase.TemplateTestCaseId &&
                categoryIds.Contains(t.TestCategoryId))
            .Select(t => t.TestCategoryId)
            .Distinct()
            .ToListAsync();

        var newTests = targetCategories
            .Where(tc => !alreadyAppliedCategoryIds.Contains(tc.TestCategoryId))
            .Select(tc => new Test
            {
                TestCategoryId = tc.TestCategoryId,
                TemplateTestCaseId = templateTestCase.TemplateTestCaseId,
                IsTemplateDerived = true,
                Name = templateTestCase.Name,
                Description = templateTestCase.Description,
                MediaUrl = templateTestCase.MediaUrl,
                ScopeStatus = templateTestCase.ScopeStatus,
                ExecutionStatus = templateTestCase.DefaultExecutionStatus,
                Production = templateTestCase.Production,
                Comments = null
            })
            .ToList();

        if (newTests.Count == 0)
        {
            return;
        }

        _ctx.Tests.AddRange(newTests);
        await _ctx.SaveChangesAsync();
    }
}
