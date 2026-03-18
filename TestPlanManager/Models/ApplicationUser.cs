using Microsoft.AspNetCore.Identity;

namespace TestPlanManager.Models;

public class ApplicationUser : IdentityUser
{
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = false;
}
