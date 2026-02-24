using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TestPlanManager.Data;
using TestPlanManager.Models;

namespace TestPlanManager.Controllers
{
    public class TestCategoryMvcController : Controller
    {
        private readonly TestPlanContext _ctx;
        public TestCategoryMvcController(TestPlanContext ctx) => _ctx = ctx;

        // list of all categories (maybe redirect to dashboard)
        public IActionResult Index()
        {
            return RedirectToAction("Index", "Home");
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

        // action to edit test status
        public async Task<IActionResult> EditTest(int id)
        {
            var test = await _ctx.Tests.FindAsync(id);
            if (test == null) return NotFound();
            return View(test);
        }

        [HttpPost]
        public async Task<IActionResult> EditTest(int TestId, int TestCategoryId, string Name, ScopeStatus ScopeStatus, ExecutionStatus ExecutionStatus, string Production, string Comments, string VideoURL)
        {
            var test = await _ctx.Tests.FindAsync(TestId);
            if (test == null) return NotFound();

            test.Name = Name;
            test.ScopeStatus = ScopeStatus;
            test.ExecutionStatus = ExecutionStatus;
            test.Production = Production ?? "";
            test.Comments = Comments ?? "";
            test.VideoURL = VideoURL ?? "";

            _ctx.Tests.Update(test);
            await _ctx.SaveChangesAsync();
            return RedirectToAction("Details", new { id = TestCategoryId });
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
        public async Task<IActionResult> CreateTest(Test model)
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
            model.VideoURL = model.VideoURL ?? "";
            
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
    }
}