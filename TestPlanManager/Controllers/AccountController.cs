using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using QRCoder;
using System.Text.Encodings.Web;
using TestPlanManager.Models;

namespace TestPlanManager.Controllers;

/// <summary>
/// ============================================================
/// ACCOUNT CONTROLLER - User Authentication & Account Management
/// ============================================================
/// Handles user login, registration, password management, and account operations.
/// This controller works with ASP.NET Core Identity for user/role management.
/// 
/// Available Actions:
/// - Login: User authentication via email/password
/// - Register: Admin creates new users
/// - RegisterSelf: Public user self-registration (Tester/Manager roles only)
/// - Logout: Sign out current user
/// - Manage: Account settings (email, password)
/// - DeleteAccount: Remove user account
/// ============================================================
/// </summary>
public class AccountController : Controller
{
    /// <summary>
    /// Allows only Tester and TestManager roles to self-register.
    /// Administrator accounts must be created by existing administrators only.
    /// </summary>
    private static readonly string[] SelfRegisterAllowedRoles = [AppRoles.Tester, AppRoles.TestManager];
    private const string AuthenticatorAppName = "TestPlanManager";

    private readonly SignInManager<ApplicationUser> _signInManager;   // Manages user sign-in operations
    private readonly UserManager<ApplicationUser> _userManager;       // Manages user accounts and passwords

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
    }

    /// <summary>
    /// GET: /Account/Login
    /// Displays the login page. No authentication required (publicly accessible).
    /// 
    /// Parameters:
    /// - returnUrl: Optional URL to redirect to after successful login
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    /// <summary>
    /// POST: /Account/Login
    /// Authenticates user with email and password.
    /// After successful login, redirects to returnUrl or TestPlanVersion/Index.
    /// Implements account lockout after 5 failed attempts.
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        // Validate form inputs
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Check if user exists and is active
        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user != null && !user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "Your account is not active. Please contact an administrator.");
            return View(model);
        }

        // Attempt sign-in with lockout on repeated failed attempts
        var result = await _signInManager.PasswordSignInAsync(
            model.Email,
            model.Password,
            model.RememberMe,              // Remember me option extends session
            lockoutOnFailure: true);       // Lock account after max failed attempts

        // Successful login - redirect to return URL or dashboard
        if (result.Succeeded)
        {
            if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return LocalRedirect(model.ReturnUrl);
            }

            return RedirectToAction("Index", "TestPlanVersion");
        }

        if (result.RequiresTwoFactor)
        {
            return RedirectToAction(nameof(LoginWith2fa), new { model.ReturnUrl, model.RememberMe });
        }

        // Account is locked due to multiple failed login attempts
        if (result.IsLockedOut)
        {
            // Re-read user to ensure we evaluate the latest lockout state.
            var lockedUser = user == null ? null : await _userManager.FindByIdAsync(user.Id);
            AddLockoutErrorMessage(lockedUser);
            return View(model);
        }

        // Invalid email or password
        ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        return View(model);
    }

    /// <summary>
    /// GET: /Account/Register
    /// Admin registration page. Only Administrators can access.
    /// Allows admins to create new user accounts with specific roles.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = AppRoles.Administrator)]
    public IActionResult Register()
    {
        return View(new RegisterViewModel { Role = AppRoles.Tester });
    }

    /// <summary>
    /// POST: /Account/Register
    /// Creates a new user account with specified role and email.
    /// This action is restricted to Administrators.
    /// Validates email uniqueness and role validity.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = AppRoles.Administrator)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        // Validate the selected role is valid
        model.Role = model.Role?.Trim() ?? string.Empty;
        if (!AppRoles.All.Contains(model.Role, StringComparer.Ordinal))
        {
            ModelState.AddModelError(nameof(model.Role), "Select a valid role.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Create new user object
        var user = new ApplicationUser
        {
            UserName = model.Email,       // Email is used as username
            Email = model.Email,
            EmailConfirmed = true,        // Skip email verification
            IsActive = true               // User is active by default
        };

        // Add user to database with password
        var result = await _userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            // Assign the selected role to the user
            var roleResult = await _userManager.AddToRoleAsync(user, model.Role);
            if (!roleResult.Succeeded)
            {
                // Rollback user creation if role assignment fails
                await _userManager.DeleteAsync(user);
                AddIdentityErrors(roleResult);
                return View(model);
            }

            TempData["SuccessMessage"] = $"User {model.Email} created with role {model.Role}.";
            return RedirectToAction("Index", "UserManagement");
        }

        AddIdentityErrors(result);
        return View(model);
    }

    /// <summary>
    /// POST: /Account/Logout
    /// Signs out the current user and clears the authentication cookie.
    /// Redirects to login page after logout.
    /// </summary>
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    /// <summary>
    /// GET: /Account/Manage
    /// Displays account management page where user can:
    /// - Update email address
    /// - Change password
    /// - Delete account
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Manage()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        return View(new ManageAccountPageViewModel
        {
            CurrentEmail = user.Email ?? user.UserName ?? string.Empty,
            IsTwoFactorEnabled = await _userManager.GetTwoFactorEnabledAsync(user),
            UpdateEmail = new UpdateEmailInputModel
            {
                NewEmail = user.Email ?? user.UserName ?? string.Empty
            }
        });
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> LoginWith2fa(bool rememberMe, string? returnUrl = null)
    {
        var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user == null)
        {
            TempData["ErrorMessage"] = "Your login session expired. Please sign in again.";
            return RedirectToAction(nameof(Login));
        }

        return View(new LoginWith2faViewModel
        {
            RememberMe = rememberMe,
            ReturnUrl = returnUrl
        });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoginWith2fa(LoginWith2faViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user == null)
        {
            TempData["ErrorMessage"] = "Your login session expired. Please sign in again.";
            return RedirectToAction(nameof(Login));
        }

        var code = model.TwoFactorCode.Replace(" ", string.Empty).Replace("-", string.Empty);
        var result = await _signInManager.TwoFactorAuthenticatorSignInAsync(code, model.RememberMe, model.RememberMachine);

        if (result.Succeeded)
        {
            if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return LocalRedirect(model.ReturnUrl);
            }

            return RedirectToAction("Index", "TestPlanVersion");
        }

        if (result.IsLockedOut)
        {
            AddLockoutErrorMessage(user);
            return View(model);
        }

        ModelState.AddModelError(string.Empty, "Invalid authenticator code.");
        return View(model);
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> EnableAuthenticator()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        if (await _userManager.GetTwoFactorEnabledAsync(user))
        {
            TempData["SuccessMessage"] = "MFA is already enabled for your account.";
            return RedirectToAction(nameof(Manage));
        }

        var model = new EnableAuthenticatorViewModel();
        await LoadAuthenticatorSetupAsync(user, model);
        return View(model);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnableAuthenticator(EnableAuthenticatorViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        if (!ModelState.IsValid)
        {
            await LoadAuthenticatorSetupAsync(user, model);
            return View(model);
        }

        var verificationCode = model.Code.Replace(" ", string.Empty).Replace("-", string.Empty);
        var isValid = await _userManager.VerifyTwoFactorTokenAsync(
            user,
            _userManager.Options.Tokens.AuthenticatorTokenProvider,
            verificationCode);

        if (!isValid)
        {
            ModelState.AddModelError(nameof(model.Code), "Verification code is invalid.");
            await LoadAuthenticatorSetupAsync(user, model);
            return View(model);
        }

        await _userManager.SetTwoFactorEnabledAsync(user, true);
        await _signInManager.RefreshSignInAsync(user);
        TempData["SuccessMessage"] = "MFA has been enabled.";
        return RedirectToAction(nameof(Manage));
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DisableAuthenticator()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        await _userManager.SetTwoFactorEnabledAsync(user, false);
        await _signInManager.RefreshSignInAsync(user);
        TempData["SuccessMessage"] = "MFA has been disabled.";
        return RedirectToAction(nameof(Manage));
    }

    /// <summary>
    /// POST: /Account/UpdateEmail
    /// Updates the user's email address.
    /// Requires current password verification and checks for duplicate emails.
    /// </summary>
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateEmail([Bind(Prefix = "UpdateEmail")] UpdateEmailInputModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        if (!ModelState.IsValid)
        {
            return View("Manage", new ManageAccountPageViewModel
            {
                CurrentEmail = user.Email ?? user.UserName ?? string.Empty,
                UpdateEmail = model,
                ChangePassword = new ChangePasswordInputModel()
            });
        }

        // Verify current password before allowing email change
        if (!await _userManager.CheckPasswordAsync(user, model.CurrentPassword))
        {
            ModelState.AddModelError("UpdateEmail.CurrentPassword", "Current password is incorrect.");
            return View("Manage", new ManageAccountPageViewModel
            {
                CurrentEmail = user.Email ?? user.UserName ?? string.Empty,
                UpdateEmail = model,
                ChangePassword = new ChangePasswordInputModel()
            });
        }

        // Check if new email is already in use by another account
        var normalizedNewEmail = model.NewEmail.Trim();
        var existingUser = await _userManager.FindByEmailAsync(normalizedNewEmail);
        if (existingUser != null && existingUser.Id != user.Id)
        {
            ModelState.AddModelError("UpdateEmail.NewEmail", "This email is already in use.");
            return View("Manage", new ManageAccountPageViewModel
            {
                CurrentEmail = user.Email ?? user.UserName ?? string.Empty,
                UpdateEmail = model,
                ChangePassword = new ChangePasswordInputModel()
            });
        }

        // Update email and username (both use email)
        user.Email = normalizedNewEmail;
        user.UserName = normalizedNewEmail;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            AddIdentityErrors(result);
            return View("Manage", new ManageAccountPageViewModel
            {
                CurrentEmail = user.Email ?? user.UserName ?? string.Empty,
                UpdateEmail = model,
                ChangePassword = new ChangePasswordInputModel()
            });
        }

        // Refresh the authentication cookie with new user information
        await _signInManager.RefreshSignInAsync(user);
        TempData["SuccessMessage"] = "Email has been updated.";
        return RedirectToAction(nameof(Manage));
    }

    /// <summary>
    /// POST: /Account/ChangePassword
    /// Allows the logged-in user to change their password.
    /// Requires verification of the current password for security.
    /// </summary>
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword([Bind(Prefix = "ChangePassword")] ChangePasswordInputModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        if (!ModelState.IsValid)
        {
            return View("Manage", new ManageAccountPageViewModel
            {
                CurrentEmail = user.Email ?? user.UserName ?? string.Empty,
                UpdateEmail = new UpdateEmailInputModel
                {
                    NewEmail = user.Email ?? user.UserName ?? string.Empty
                },
                ChangePassword = model
            });
        }

        // Change password using Identity API (validates old password)
        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            AddIdentityErrors(result);
            return View("Manage", new ManageAccountPageViewModel
            {
                CurrentEmail = user.Email ?? user.UserName ?? string.Empty,
                UpdateEmail = new UpdateEmailInputModel
                {
                    NewEmail = user.Email ?? user.UserName ?? string.Empty
                },
                ChangePassword = model
            });
        }

        // Refresh authentication cookie after password change
        await _signInManager.RefreshSignInAsync(user);
        TempData["SuccessMessage"] = "Password has been updated.";
        return RedirectToAction(nameof(Manage));
    }

    /// <summary>
    /// POST: /Account/DeleteAccount
    /// Permanently deletes the user's account.
    /// Prevents deletion of the last administrator account for security.
    /// </summary>
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAccount()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        // Prevent deletion of the last administrator account
        if (await _userManager.IsInRoleAsync(user, AppRoles.Administrator))
        {
            var administrators = await _userManager.GetUsersInRoleAsync(AppRoles.Administrator);
            if (administrators.Count <= 1)
            {
                TempData["ErrorMessage"] = "You cannot delete the last administrator account.";
                return RedirectToAction("Index", "UserManagement");
            }
        }

        // Delete the user account
        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = string.Join("; ", result.Errors.Select(e => e.Description));
            return RedirectToAction("Index", "Home");
        }

        // Sign out user after account deletion
        await _signInManager.SignOutAsync();
        TempData["SuccessMessage"] = "Your account has been deleted.";
        return RedirectToAction(nameof(Login));
    }

    /// <summary>
    /// GET: /Account/RegisterSelf
    /// Public registration page where users can self-register.
    /// Only Tester and TestManager roles are allowed (not Administrator).
    /// New accounts are created in inactive state and require admin approval.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public IActionResult RegisterSelf()
    {
        return View(new RegisterSelfViewModel { Role = AppRoles.Tester });
    }

    /// <summary>
    /// POST: /Account/RegisterSelf
    /// Creates a self-registered user account.
    /// Default role is Tester, but TestManager is also allowed.
    /// Account is marked as inactive and requires admin approval.
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterSelf(RegisterSelfViewModel model)
    {
        // Validate that selected role is in allowed roles
        model.Role = model.Role?.Trim() ?? string.Empty;

        if (!SelfRegisterAllowedRoles.Contains(model.Role, StringComparer.Ordinal))
        {
            ModelState.AddModelError(nameof(model.Role), "You can only register as a Tester or Test Manager.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Create new user with inactive status (pending admin approval)
        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            EmailConfirmed = true,
            IsActive = false  // Must be activated by administrator
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            var roleResult = await _userManager.AddToRoleAsync(user, model.Role);
            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);
                AddIdentityErrors(roleResult);
                return View(model);
            }

            TempData["SuccessMessage"] = $"Account created successfully! Your account is pending activation by an administrator.";
            return RedirectToAction(nameof(Login));
        }

        AddIdentityErrors(result);
        return View(model);
    }

    /// <summary>
    /// GET: /Account/AccessDenied
    /// Displays access denied message when user lacks required permissions or roles.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }

    /// <summary>
    /// Helper method: Adds Identity error messages to ModelState for display to user.
    /// Used when user creation, password operations, or email updates fail.
    /// </summary>
    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }
    }

    /// <summary>
    /// Helper method: Determines lockout reason and adds appropriate error message.
    /// Distinguishes between permanent admin lockout and temporary failed attempt lockout.
    /// </summary>
    private void AddLockoutErrorMessage(ApplicationUser? lockedUser)
    {
        if (lockedUser?.LockoutEnd.HasValue == true)
        {
            var lockoutEnd = lockedUser.LockoutEnd.Value;
            var oneYearFromNow = DateTimeOffset.UtcNow.AddYears(1);

            // If lockout is more than 1 year in future, it's a permanent admin lockout
            if (lockoutEnd > oneYearFromNow)
            {
                ModelState.AddModelError(string.Empty, "Your account has been locked by an administrator. Please contact support.");
                return;
            }

            // Temporary lockout due to repeated failed login attempts
            ModelState.AddModelError(string.Empty, "Account is temporarily locked due to repeated failed sign-in attempts.");
            return;
        }

        // Generic lockout message if details unavailable
        ModelState.AddModelError(string.Empty, "Account is temporarily locked. Please try again later.");
    }

    private async Task LoadAuthenticatorSetupAsync(ApplicationUser user, EnableAuthenticatorViewModel model)
    {
        var unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrWhiteSpace(unformattedKey))
        {
            await _userManager.ResetAuthenticatorKeyAsync(user);
            unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
        }

        model.SharedKey = FormatKey(unformattedKey ?? string.Empty);
        model.AuthenticatorUri = GenerateQrCodeUri(user.Email ?? user.UserName ?? "user", unformattedKey ?? string.Empty);
        model.QrCodeImageDataUrl = GenerateQrCodeImageDataUrl(model.AuthenticatorUri);
    }

    private static string FormatKey(string unformattedKey)
    {
        if (string.IsNullOrWhiteSpace(unformattedKey))
        {
            return string.Empty;
        }

        var result = new System.Text.StringBuilder();
        var currentPosition = 0;
        while (currentPosition + 4 < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition, 4)).Append(' ');
            currentPosition += 4;
        }

        if (currentPosition < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition));
        }

        return result.ToString().ToLowerInvariant();
    }

    private static string GenerateQrCodeUri(string email, string unformattedKey)
    {
        var issuer = UrlEncoder.Default.Encode(AuthenticatorAppName);
        var encodedEmail = UrlEncoder.Default.Encode(email);
        return $"otpauth://totp/{issuer}:{encodedEmail}?secret={unformattedKey}&issuer={issuer}&digits=6";
    }

    private static string GenerateQrCodeImageDataUrl(string uri)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(uri, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(qrCodeData);
        var qrCodeBytes = qrCode.GetGraphic(20);
        return $"data:image/png;base64,{Convert.ToBase64String(qrCodeBytes)}";
    }
}
