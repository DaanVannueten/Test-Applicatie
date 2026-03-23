using Microsoft.AspNetCore.Identity;

namespace TestPlanManager.Models;

/// <summary>
/// ============================================================
/// APPLICATION USER MODEL - Extended Identity User
/// ============================================================
/// Custom user class extending ASP.NET Core Identity IdentityUser.
/// Adds application-specific properties to track account status and creation time.
/// 
/// Inherited from IdentityUser:
/// - Id, UserName, Email, PasswordHash, PhoneNumber, etc.
/// - AccessFailedCount, LockoutEnabled, LockoutEnd, etc.
/// 
/// Additional Properties:
/// - CreatedAtUtc: When the account was created
/// - IsActive: Whether the account is enabled/disabled
/// 
/// Default Roles:
/// - Administrator: Full system access
/// - TestManager: Manage test plans and users
/// - Tester: Execute tests and view results
/// ============================================================
/// </summary>
public class ApplicationUser : IdentityUser
{
    /// <summary>
    /// Account creation timestamp in UTC.
    /// Used to track when user accounts were created.
    /// Automatically set to DateTime.UtcNow when account is created.
    /// Useful for audit trails and account history.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Flag indicating if the user account is active/enabled.
    /// Default value: false (accounts start inactive, require admin approval)
    /// 
    /// When false:
    /// - User cannot log in even with correct password
    /// - Login attempt shows "account not active" error
    /// - Requires administrator to set IsActive = true to enable
    /// 
    /// Used for:
    /// - Soft-deleting accounts (mark inactive instead of delete)
    /// - Pending activation workflow for self-registered users
    /// - Temporarily disabling accounts without deletion
    /// </summary>
    public bool IsActive { get; set; } = false;
}
