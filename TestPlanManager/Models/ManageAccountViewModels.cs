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
    [Required]
    [Display(Name = "Authenticator code")]
    public string TwoFactorCode { get; set; } = string.Empty;

    public bool RememberMe { get; set; }

    [Display(Name = "Remember this device")]
    public bool RememberMachine { get; set; }

    public string? ReturnUrl { get; set; }
}

public class EnableAuthenticatorViewModel
{
    [Required]
    [StringLength(7, ErrorMessage = "Code must be 6 digits.", MinimumLength = 6)]
    [Display(Name = "Verification code")]
    public string Code { get; set; } = string.Empty;

    public string SharedKey { get; set; } = string.Empty;
    public string AuthenticatorUri { get; set; } = string.Empty;
    public string QrCodeImageDataUrl { get; set; } = string.Empty;
}

public class ResetUserMfaInputModel
{
    [Required]
    public string UserId { get; set; } = string.Empty;
}

public class UpdateEmailInputModel
{
    [Required]
    [EmailAddress]
    [Display(Name = "New Email")]
    public string NewEmail { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Current Password")]
    public string CurrentPassword { get; set; } = string.Empty;
}

public class ChangePasswordInputModel
{
    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Current Password")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [MinLength(8)]
    [Display(Name = "New Password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm New Password")]
    [Compare(nameof(NewPassword), ErrorMessage = "New password and confirmation do not match.")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}