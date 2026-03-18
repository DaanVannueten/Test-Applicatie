using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TestPlanManager.Models;

namespace TestPlanManager.Controllers;

public class AccountController : Controller
{
    private static readonly string[] SelfRegisterAllowedRoles = [AppRoles.Tester, AppRoles.TestManager];

    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user != null && !user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "Your account is not active. Please contact an administrator.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            model.Email,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: true);

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
            // Re-read user to ensure we evaluate the latest lockout state.
            var lockedUser = user == null ? null : await _userManager.FindByIdAsync(user.Id);
            AddLockoutErrorMessage(lockedUser);
            return View(model);
        }

        ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        return View(model);
    }

    [HttpGet]
    [Authorize(Roles = AppRoles.Administrator)]
    public IActionResult Register()
    {
        return View(new RegisterViewModel { Role = AppRoles.Tester });
    }

    [HttpPost]
    [Authorize(Roles = AppRoles.Administrator)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        model.Role = model.Role?.Trim() ?? string.Empty;
        if (!AppRoles.All.Contains(model.Role, StringComparer.Ordinal))
        {
            ModelState.AddModelError(nameof(model.Role), "Select a valid role.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            EmailConfirmed = true,
            IsActive = true
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

            TempData["SuccessMessage"] = $"User {model.Email} created with role {model.Role}.";
            return RedirectToAction("Index", "UserManagement");
        }

        AddIdentityErrors(result);

        return View(model);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

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
            UpdateEmail = new UpdateEmailInputModel
            {
                NewEmail = user.Email ?? user.UserName ?? string.Empty
            }
        });
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateEmail(UpdateEmailInputModel model)
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

        await _signInManager.RefreshSignInAsync(user);
        TempData["SuccessMessage"] = "Email has been updated.";
        return RedirectToAction(nameof(Manage));
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordInputModel model)
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

        await _signInManager.RefreshSignInAsync(user);
        TempData["SuccessMessage"] = "Password has been updated.";
        return RedirectToAction(nameof(Manage));
    }

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

        if (await _userManager.IsInRoleAsync(user, AppRoles.Administrator))
        {
            var administrators = await _userManager.GetUsersInRoleAsync(AppRoles.Administrator);
            if (administrators.Count <= 1)
            {
                TempData["ErrorMessage"] = "You cannot delete the last administrator account.";
                return RedirectToAction("Index", "UserManagement");
            }
        }

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = string.Join("; ", result.Errors.Select(e => e.Description));
            return RedirectToAction("Index", "Home");
        }

        await _signInManager.SignOutAsync();
        TempData["SuccessMessage"] = "Your account has been deleted.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult RegisterSelf()
    {
        return View(new RegisterSelfViewModel { Role = AppRoles.Tester });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterSelf(RegisterSelfViewModel model)
    {
        model.Role = model.Role?.Trim() ?? string.Empty;

        if (!SelfRegisterAllowedRoles.Contains(model.Role, StringComparer.Ordinal))
        {
            ModelState.AddModelError(nameof(model.Role), "You can only register as a Tester or Test Manager.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            EmailConfirmed = true,
            IsActive = false
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

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }
    }

    private void AddLockoutErrorMessage(ApplicationUser? lockedUser)
    {
        if (lockedUser?.LockoutEnd.HasValue == true)
        {
            var lockoutEnd = lockedUser.LockoutEnd.Value;
            var oneYearFromNow = DateTimeOffset.UtcNow.AddYears(1);

            if (lockoutEnd > oneYearFromNow)
            {
                ModelState.AddModelError(string.Empty, "Your account has been locked by an administrator. Please contact support.");
                return;
            }

            ModelState.AddModelError(string.Empty, "Account is temporarily locked due to repeated failed sign-in attempts.");
            return;
        }

        ModelState.AddModelError(string.Empty, "Account is temporarily locked. Please try again later.");
    }
}
