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
    public class SprintController : ControllerBase
    {
        private readonly TestPlanContext _ctx;
        public SprintController(TestPlanContext ctx) => _ctx = ctx;

        [HttpGet]
        public async Task<IEnumerable<Sprint>> GetAll() =>
            await _ctx.Sprints.Include(s => s.TestCategories).ToListAsync();

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var sprint = await _ctx.Sprints
                .Include(s => s.TestCategories)
                .FirstOrDefaultAsync(s => s.SprintId == id);
            return sprint == null ? NotFound() : Ok(sprint);
        }

        [HttpPost]
        [Authorize(Roles = AppRoles.Admin)]
        public async Task<IActionResult> Create(Sprint sprint)
        {
            _ctx.Sprints.Add(sprint);
            await _ctx.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = sprint.SprintId }, sprint);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = AppRoles.Admin)]
        public async Task<IActionResult> Update(int id, Sprint update)
        {
            if (id != update.SprintId) return BadRequest();
            _ctx.Entry(update).State = EntityState.Modified;
            await _ctx.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = AppRoles.Admin)]
        public async Task<IActionResult> Delete(int id)
        {
            var sprint = await _ctx.Sprints.FindAsync(id);
            if (sprint == null) return NotFound();
            _ctx.Sprints.Remove(sprint);
            await _ctx.SaveChangesAsync();
            return NoContent();
        }
    }
}