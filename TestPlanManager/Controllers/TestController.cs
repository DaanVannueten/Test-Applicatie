using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TestPlanManager.Data;
using TestPlanManager.Models;

namespace TestPlanManager.Controllers
{
    /// <summary>
    /// ============================================================
    /// TEST API CONTROLLER - RESTful API for Test Operations
    /// ============================================================
    /// Provides REST API endpoints for test management and execution status updates.
    /// This is an API controller (returns JSON) as opposed to MVC controller (returns HTML views).
    /// 
    /// Base Route: /api/test
    /// All endpoints require authentication [Authorize] attribute
    /// 
    /// Available Endpoints:
    /// - GET /api/test/{id} - Retrieve a single test by ID
    /// - POST /api/test - Create a new test (Managers/Admins only)
    /// - PUT /api/test/{id}/status - Update test execution status
    /// ============================================================
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly TestPlanContext _ctx;  // Database context for data access

        public TestController(TestPlanContext ctx) => _ctx = ctx;

        /// <summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var t = await _ctx.Tests
                .AsNoTracking()
                .FirstOrDefaultAsync(test => test.TestId == id);

            return t == null ? NotFound() : Ok(t);
        }

        /// <summary>
        [HttpPost]
        [Authorize(Roles = AppRoles.Managers)]
        public async Task<IActionResult> Create(Test test)
        {
            _ctx.Tests.Add(test);
            await _ctx.SaveChangesAsync();

            return CreatedAtAction(nameof(Get), new { id = test.TestId }, test);
        }

        /// <summary>
        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] TestStatusUpdateDto dto)
        {
            var t = await _ctx.Tests.FindAsync(id);
            if (t == null) return NotFound();

            var category = await _ctx.TestCategories
                .Include(tc => tc.Tests)
                .FirstOrDefaultAsync(tc => tc.TestCategoryId == t.TestCategoryId);
            if (category == null) return NotFound();

            var requestedMediaUrl = dto.MediaUrl ?? dto.VideoURL;
            TestExecutionHelper.ApplyExecution(
                t,
                dto.ExecutionStatus,
                dto.Production,
                dto.Comments,
                requestedMediaUrl,
                User.FindFirst(ClaimTypes.Email)?.Value ?? User?.Identity?.Name,
                DateTime.UtcNow);

            category.TestDate = TestExecutionHelper.GetLatestExecutionDate(category.Tests);

            await _ctx.SaveChangesAsync();
            return NoContent();
        }

        /// <summary>
        public class TestStatusUpdateDto
        {
            public ExecutionStatus ExecutionStatus { get; set; }

            public string? Production { get; set; }

            public string? Comments { get; set; }

            public string? MediaUrl { get; set; }

            public string? VideoURL { get; set; }
        }
    }
}
