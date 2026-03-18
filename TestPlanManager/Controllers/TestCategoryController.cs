using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TestPlanManager.Data;
using TestPlanManager.Models;

namespace TestPlanManager.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class TestCategoryController : ControllerBase
    {
        private readonly TestPlanContext _ctx;
        public TestCategoryController(TestPlanContext ctx) => _ctx = ctx;

        [HttpGet("{id}")]
        public async Task<IActionResult> GetDetail(int id)
        {
            var cat = await _ctx.TestCategories
                .AsNoTracking()
                .Include(tc => tc.Tests)
                .FirstOrDefaultAsync(tc => tc.TestCategoryId == id);

            if (cat == null) return NotFound();
            cat.Recalculate();
            return Ok(cat);
        }

        [HttpGet("sprint/{sprintId}/summary")]
        public async Task<IActionResult> SprintSummary(int sprintId)
        {
            var categories = await _ctx.TestCategories
                .AsNoTracking()
                .Where(tc => tc.SprintId == sprintId)
                .Select(tc => new
                {
                    tc.TestCategoryId,
                    tc.Name,
                    tc.Department,
                    tc.Passed,
                    tc.Failed,
                    tc.Blocked,
                    tc.OutOfScope,
                    tc.TotalTest,
                    tc.PercentagePassed
                })
                .ToListAsync();

            return Ok(categories);
        }

        [HttpPost]
        [Authorize(Roles = AppRoles.Managers)]
        public async Task<IActionResult> Create(TestCategory cat)
        {
            _ctx.TestCategories.Add(cat);
            await _ctx.SaveChangesAsync();
            return CreatedAtAction(nameof(GetDetail), new { id = cat.TestCategoryId }, cat);
        }
    }
}