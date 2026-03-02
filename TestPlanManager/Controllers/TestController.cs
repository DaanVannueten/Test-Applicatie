using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TestPlanManager.Data;
using TestPlanManager.Models;

namespace TestPlanManager.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly TestPlanContext _ctx;
        public TestController(TestPlanContext ctx) => _ctx = ctx;

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var t = await _ctx.Tests.FindAsync(id);
            return t == null ? NotFound() : Ok(t);
        }

        [HttpPost]
        public async Task<IActionResult> Create(Test test)
        {
            _ctx.Tests.Add(test);
            await _ctx.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = test.TestId }, test);
        }

        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] TestStatusUpdateDto dto)
        {
            var t = await _ctx.Tests.FindAsync(id);
            if (t == null) return NotFound();

            var category = await _ctx.TestCategories
                .Include(tc => tc.Tests)
                .FirstOrDefaultAsync(tc => tc.TestCategoryId == t.TestCategoryId);
            if (category == null) return NotFound();

            t.ExecutionStatus = dto.ExecutionStatus;
            t.Production = dto.Production;
            t.Comments = dto.Comments;
            t.VideoURL = dto.VideoURL;

            if (dto.ExecutionStatus != ExecutionStatus.NotRun)
            {
                category.TestDate = DateTime.UtcNow;
            }
            else
            {
                var hasAnyExecuted = category.Tests
                    .Where(test => test.TestId != t.TestId)
                    .Any(test => test.ExecutionStatus != ExecutionStatus.NotRun);

                category.TestDate = hasAnyExecuted ? category.TestDate : null;
            }

            await _ctx.SaveChangesAsync();
            return NoContent();
        }

        public class TestStatusUpdateDto
        {
            public ExecutionStatus ExecutionStatus { get; set; }
            public string? Production { get; set; }
            public string? Comments { get; set; }
            public string? VideoURL { get; set; }
        }
    }
}