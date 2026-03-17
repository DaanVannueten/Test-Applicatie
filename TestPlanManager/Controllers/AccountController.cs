using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TestPlanManager.Models;

namespace TestPlanManager.Controllers;

public class AccountController : Controller
{
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
            // Check if this is an admin lock (LockoutEnd far in future) or a failed attempts lock
            if (user?.LockoutEnd.HasValue == true)
            {
                var lockoutEnd = user.LockoutEnd.Value;
                var oneYearFromNow = DateTimeOffset.UtcNow.AddYears(1);
                
                if (lockoutEnd > oneYearFromNow)
                {
                    // Admin lock
                    ModelState.AddModelError(string.Empty, "Your account has been locked by an administrator. Please contact support.");
                }
                else
                {
                    // Failed attempts lock
                    ModelState.AddModelError(string.Empty, "Account is temporarily locked due to repeated failed sign-in attempts.");
                }
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Account is temporarily locked. Please try again later.");
            }
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
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            var roleResult = await _userManager.AddToRoleAsync(user, model.Role);
            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);
                foreach (var roleError in roleResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, roleError.Description);
                }

                return View(model);
            }

            TempData["SuccessMessage"] = $"User {model.Email} created with role {model.Role}.";
            return RedirectToAction("Index", "UserManagement");
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

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
        
        // Only allow Tester and TestManager roles for self-registration
        if (!new[] { AppRoles.Tester, AppRoles.TestManager }.Contains(model.Role, StringComparer.Ordinal))
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
            IsActive = false // New accounts are inactive by default
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            var roleResult = await _userManager.AddToRoleAsync(user, model.Role);
            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);
                foreach (var roleError in roleResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, roleError.Description);
                }

                return View(model);
            }

            TempData["SuccessMessage"] = $"Account created successfully! Your account is pending activation by an administrator.";
            return RedirectToAction(nameof(Login));
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }
}
