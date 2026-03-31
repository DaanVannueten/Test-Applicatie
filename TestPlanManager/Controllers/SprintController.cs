using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TestPlanManager.Data;
using TestPlanManager.Models;

namespace TestPlanManager.Controllers
{
    /// <summary>
    /// ============================================================
    /// SPRINT API CONTROLLER - RESTful API for Sprint Management
    /// ============================================================
    /// Provides REST API endpoints for CRUD operations on test cycles/sprints.
    /// A Sprint represents a period of testing (test cycle version with build number).
    /// 
    /// Base Route: /api/sprint
    /// All endpoints require authentication [Authorize] attribute
    /// 
    /// Available Endpoints:
    /// - GET /api/sprint - List all sprints with categories
    /// - GET /api/sprint/{id} - Retrieve a specific sprint
    /// - POST /api/sprint - Create new sprint (Admin only)
    /// - PUT /api/sprint/{id} - Update sprint (Admin only)
    /// - DELETE /api/sprint/{id} - Delete sprint (Admin only)
    /// ============================================================
    /// </summary>
    [ApiController]
    [Authorize]  // All endpoints require authenticated user
    [Route("api/[controller]")]
    public class SprintController : ControllerBase
    {
        private readonly TestPlanContext _ctx;  // Database context for data access

        public SprintController(TestPlanContext ctx) => _ctx = ctx;

        /// <summary>
        /// GET: /api/sprint
        /// Retrieves all sprints with their associated test categories.
        /// 
        /// Returns:
        /// - 200 OK with array of sprint objects (read-only, no tracking)
        /// </summary>
        [HttpGet]
        public async Task<IEnumerable<Sprint>> GetAll() =>
            await _ctx.Sprints
                .AsNoTracking()                    // Read-only, no change tracking
                .Include(s => s.TestCategories)   // Include related categories
                .ToListAsync();

        /// <summary>
        /// GET: /api/sprint/{id}
        /// Retrieves a specific sprint by ID with its test categories.
        /// 
        /// Parameters:
        /// - id: The SprintId to retrieve
        /// 
        /// Returns:
        /// - 200 OK with sprint object
        /// - 404 Not Found if sprint doesn't exist
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var sprint = await _ctx.Sprints
                .AsNoTracking()
                .Include(s => s.TestCategories)
                .FirstOrDefaultAsync(s => s.SprintId == id);

            return sprint == null ? NotFound() : Ok(sprint);
        }

        /// <summary>
        /// POST: /api/sprint
        /// Creates a new sprint record.
        /// Restricted to Administrators only.
        /// 
        /// Returns:
        /// - 201 Created with sprint object and location header
        /// </summary>
        [HttpPost]
        [Authorize(Roles = AppRoles.Administrator)]  // Only admins can create sprints
        public async Task<IActionResult> Create(Sprint sprint)
        {
            _ctx.Sprints.Add(sprint);
            await _ctx.SaveChangesAsync();

            // Return 201 Created status with location of new resource
            return CreatedAtAction(nameof(Get), new { id = sprint.SprintId }, sprint);
        }

        /// <summary>
        /// PUT: /api/sprint/{id}
        /// Updates an existing sprint record.
        /// Restricted to Administrators only.
        /// 
        /// Parameters:
        /// - id: The SprintId to update
        /// - update: Sprint object with new values
        /// 
        /// Returns:
        /// - 204 No Content on success
        /// - 400 Bad Request if IDs don't match
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = AppRoles.Administrator)]  // Only admins can update sprints
        public async Task<IActionResult> Update(int id, Sprint update)
        {
            // Verify URL ID matches object ID
            if (id != update.SprintId) return BadRequest();

            // Mark entity as modified and save
            _ctx.Entry(update).State = EntityState.Modified;
            await _ctx.SaveChangesAsync();

            return NoContent();  // 204 No Content
        }

        /// <summary>
        /// DELETE: /api/sprint/{id}
        /// Deletes a sprint record.
        /// Restricted to Administrators only.
        /// Cascading behavior: associated TestCategories and Tests are also deleted.
        /// 
        /// Parameters:
        /// - id: The SprintId to delete
        /// 
        /// Returns:
        /// - 204 No Content on success
        /// - 404 Not Found if sprint doesn't exist
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = AppRoles.Administrator)]  // Only admins can delete sprints
        public async Task<IActionResult> Delete(int id)
        {
            var sprint = await _ctx.Sprints.FindAsync(id);
            if (sprint == null) return NotFound();

            _ctx.Sprints.Remove(sprint);
            await _ctx.SaveChangesAsync();

            return NoContent();  // 204 No Content
        }
    }
}