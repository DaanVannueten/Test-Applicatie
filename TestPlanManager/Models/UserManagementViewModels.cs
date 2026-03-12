using System.ComponentModel.DataAnnotations;

namespace TestPlanManager.Models;

public class UserManagementPageViewModel
{
    public List<UserAccountRowViewModel> Users { get; set; } = [];
}

public class UserAccountRowViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsLockedOut { get; set; }
}

public class UpdateUserRoleInputModel
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = string.Empty;
}

public class UpdateUserLockInputModel
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    public bool LockUser { get; set; }
}
