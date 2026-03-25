using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
    [Authorize]  // All endpoints require authenticated user
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly TestPlanContext _ctx;  // Database context for data access
        
        public TestController(TestPlanContext ctx) => _ctx = ctx;

        /// <summary>
        /// GET: /api/test/{id}
        /// Retrieves a specific test by ID.
        /// 
        /// parameters:
        /// - id: The TestId to retrieve
        /// 
        /// Returns:
        /// - 200 OK with test object
        /// - 404 Not Found if test doesn't exist
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            // Query test without tracking (read-only operation)
            var t = await _ctx.Tests
                .AsNoTracking()
                .FirstOrDefaultAsync(test => test.TestId == id);
            
            // Return 404 if not found, otherwise return test data
            return t == null ? NotFound() : Ok(t);
        }

        /// <summary>
        /// POST: /api/test
        /// Creates a new test record.
        /// Restricted to Managers and Administrators only.
        /// 
        /// Returns:
        /// - 201 Created with test object and location header
        /// </summary>
        [HttpPost]
        [Authorize(Roles = AppRoles.Managers)]  // Only managers and admins can create
        public async Task<IActionResult> Create(Test test)
        {
            _ctx.Tests.Add(test);
            await _ctx.SaveChangesAsync();
            
            // Return 201 Created status with location of new resource
            return CreatedAtAction(nameof(Get), new { id = test.TestId }, test);
        }

        /// <summary>
        /// PUT: /api/test/{id}/status
        /// Updates a test's execution status (Passed/Failed/Blocked/NotRun).
        /// Also updates associated metadata: execution timestamp, production info, comments, media.
        /// Updates the parent category's TestDate based on latest executed test.
        /// 
        /// Parameters:
        /// - id: The TestId to update
        /// - dto: Status update details (ExecutionStatus, Production, Comments, MediaUrl)
        /// 
        /// Returns:
        /// - 204 No Content on success
        /// - 404 Not Found if test or category doesn't exist
        /// </summary>
        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] TestStatusUpdateDto dto)
        {
            // Load the test to update
            var t = await _ctx.Tests.FindAsync(id);
            if (t == null) return NotFound();

            // Store previous status to detect transition to Executed state
            var previousStatus = t.ExecutionStatus;

            // Load parent category and all its tests (needed to recalculate category's TestDate)
            var category = await _ctx.TestCategories
                .Include(tc => tc.Tests)
                .FirstOrDefaultAsync(tc => tc.TestCategoryId == t.TestCategoryId);
            if (category == null) return NotFound();

            // Update test status and metadata
            t.ExecutionStatus = dto.ExecutionStatus;
            t.Production = dto.Production ?? "";           // Production environment info (version, build, etc.)
            t.Comments = dto.Comments;                     // Test execution comments
            var requestedMediaUrl = dto.MediaUrl ?? dto.VideoURL;
            if (requestedMediaUrl is not null)
            {
                t.MediaUrl = requestedMediaUrl;  // Only overwrite when caller explicitly sends media
            }

            // Set or clear execution timestamp based on new status
            if (dto.ExecutionStatus != ExecutionStatus.NotRun)
            {
                // If transitioning from NotRun to Executed, set current time
                if (previousStatus == ExecutionStatus.NotRun || !t.ExecutedAt.HasValue)
                {
                    t.ExecutedAt = DateTime.UtcNow;
                }
            }
            else
            {
                // Clear execution timestamp if reverting to NotRun
                t.ExecutedAt = null;
            }

            // Recalculate category's TestDate - should be the latest executed test time
            category.TestDate = category.Tests
                .Where(test => test.ExecutionStatus != ExecutionStatus.NotRun && test.ExecutedAt.HasValue)
                .Select(test => test.ExecutedAt)
                .Max();  // Returns null if no executed tests

            // Persist all changes to database
            await _ctx.SaveChangesAsync();
            return NoContent();  // 204 No Content
        }

        /// <summary>
        /// Data Transfer Object (DTO) for test status updates.
        /// Used for PUT /api/test/{id}/status endpoint.
        /// </summary>
        public class TestStatusUpdateDto
        {
            /// <summary>
            /// The new execution status: Passed, Failed, Blocked, or NotRun
            /// </summary>
            public ExecutionStatus ExecutionStatus { get; set; }
            
            /// <summary>
            /// Production environment information (e.g., "v1.2.3", "Build 456")
            /// </summary>
            public string? Production { get; set; }
            
            /// <summary>
            /// Test execution comments or notes
            /// </summary>
            public string? Comments { get; set; }
            
            /// <summary>
            /// URL to test evidence (screenshot, video, etc.)
            /// </summary>
            public string? MediaUrl { get; set; }
            
            /// <summary>
            /// Legacy property name for MediaUrl (kept for backward compatibility)
            /// </summary>
            public string? VideoURL { get; set; }
        }
    }
}