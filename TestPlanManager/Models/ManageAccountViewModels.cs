using System.ComponentModel.DataAnnotations;

namespace TestPlanManager.Models;

public class ManageAccountPageViewModel
{
    public string CurrentEmail { get; set; } = string.Empty;
    public bool IsTwoFactorEnabled { get; set; }
    public UpdateEmailInputModel UpdateEmail { get; set; } = new();
    public ChangePasswordInputModel ChangePassword { get; set; } = new();
}

public class LoginWith2faViewModel
{
    [Required(ErrorMessage = "Authenticator code is required")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Code must be exactly 6 digits")]
    [Display(Name = "Authenticator code")]
    public string TwoFactorCode { get; set; } = string.Empty;

    public bool RememberMe { get; set; }

    [Display(Name = "Remember this device")]
    public bool RememberMachine { get; set; }

    public string? ReturnUrl { get; set; }
}

public class EnableAuthenticatorViewModel
{
    [Required(ErrorMessage = "Verification code is required")]
    [StringLength(7, ErrorMessage = "Code must be 6 digits", MinimumLength = 6)]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Code must be exactly 6 digits")]
    [Display(Name = "Verification code")]
    public string Code { get; set; } = string.Empty;

    public string SharedKey { get; set; } = string.Empty;
    public string AuthenticatorUri { get; set; } = string.Empty;
    public string QrCodeImageDataUrl { get; set; } = string.Empty;
}

public class ResetUserMfaInputModel
{
    [Required(ErrorMessage = "User ID is required")]
    [StringLength(450, ErrorMessage = "User ID cannot exceed 450 characters")]
    public string UserId { get; set; } = string.Empty;
}

public class UpdateEmailInputModel
{
    [Required(ErrorMessage = "Email address is required")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address")]
    [StringLength(256, ErrorMessage = "Email cannot exceed 256 characters")]
    [Display(Name = "New Email")]
    public string NewEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Current password is required to change email")]
    [DataType(DataType.Password)]
    [StringLength(128, ErrorMessage = "Password cannot exceed 128 characters")]
    [Display(Name = "Current Password")]
    public string CurrentPassword { get; set; } = string.Empty;
}

public class ChangePasswordInputModel
{
    [Required(ErrorMessage = "Current password is required")]
    [DataType(DataType.Password)]
    [StringLength(128, ErrorMessage = "Password cannot exceed 128 characters")]
    [Display(Name = "Current Password")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "New password is required")]
    [DataType(DataType.Password)]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters long")]
    [StringLength(128, ErrorMessage = "Password cannot exceed 128 characters")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).+$", 
        ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, one digit, and one special character")]
    [Display(Name = "New Password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your new password")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "New password and confirmation do not match")]
    [Display(Name = "Confirm New Password")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}