using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TestPlanManager.Models;

namespace TestPlanManager.Controllers;

[Authorize(Roles = AppRoles.Administrator)]
public class UserManagementController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UserManagementController(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var currentUserId = _userManager.GetUserId(User);

        var users = await _userManager.Users
            .AsNoTracking()
            .OrderBy(u => u.CreatedAtUtc)
            .ThenBy(u => u.Email)
            .ToListAsync();

        var vm = new UserManagementPageViewModel();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            vm.Users.Add(new UserAccountRowViewModel
            {
                UserId = user.Id,
                Email = user.Email ?? user.UserName ?? string.Empty,
                Role = roles.FirstOrDefault() ?? string.Empty,
                IsActive = user.IsActive,
                IsMfaEnabled = user.TwoFactorEnabled,
                IsCurrentUser = user.Id == currentUserId
            });
        }

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateRole(UpdateUserRoleInputModel model)
    {
        model.Role = model.Role?.Trim() ?? string.Empty;
        if (!ModelState.IsValid || !AppRoles.All.Contains(model.Role, StringComparer.Ordinal))
        {
            TempData["ErrorMessage"] = "Select a valid user and role.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _userManager.FindByIdAsync(model.UserId);
        if (user == null)
        {
            TempData["ErrorMessage"] = "User not found.";
            return RedirectToAction(nameof(Index));
        }

        var currentUserId = _userManager.GetUserId(User);
        if (user.Id == currentUserId && !string.Equals(model.Role, AppRoles.Administrator, StringComparison.Ordinal))
        {
            TempData["ErrorMessage"] = "You cannot remove your own Administrator role.";
            return RedirectToAction(nameof(Index));
        }

        var existingRoles = await _userManager.GetRolesAsync(user);
        if (existingRoles.Count > 0)
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, existingRoles);
            if (!removeResult.Succeeded)
            {
                TempData["ErrorMessage"] = string.Join("; ", removeResult.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Index));
            }
        }

        var addResult = await _userManager.AddToRoleAsync(user, model.Role);
        if (!addResult.Succeeded)
        {
            TempData["ErrorMessage"] = string.Join("; ", addResult.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Index));
        }

        TempData["SuccessMessage"] = $"Role for {user.Email} updated to {model.Role}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateActive(UpdateUserActiveStatusInputModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Invalid request.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _userManager.FindByIdAsync(model.UserId);
        if (user == null)
        {
            TempData["ErrorMessage"] = "User not found.";
            return RedirectToAction(nameof(Index));
        }

        var currentUserId = _userManager.GetUserId(User);
        if (user.Id == currentUserId && !model.IsActive)
        {
            TempData["ErrorMessage"] = "You cannot deactivate your own account.";
            return RedirectToAction(nameof(Index));
        }

        user.IsActive = model.IsActive;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = string.Join("; ", result.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Index));
        }

        TempData["SuccessMessage"] = model.IsActive
            ? $"User {user.Email} has been activated."
            : $"User {user.Email} has been deactivated.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(DeleteUserInputModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Invalid request.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _userManager.FindByIdAsync(model.UserId);
        if (user == null)
        {
            TempData["ErrorMessage"] = "User not found.";
            return RedirectToAction(nameof(Index));
        }

        var currentUserId = _userManager.GetUserId(User);
        if (user.Id == currentUserId)
        {
            TempData["ErrorMessage"] = "You cannot delete your own account from User Management.";
            return RedirectToAction(nameof(Index));
        }

        if (await _userManager.IsInRoleAsync(user, AppRoles.Administrator))
        {
            var administrators = await _userManager.GetUsersInRoleAsync(AppRoles.Administrator);
            if (administrators.Count <= 1)
            {
                TempData["ErrorMessage"] = "You cannot delete the last administrator account.";
                return RedirectToAction(nameof(Index));
            }
        }

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = string.Join("; ", result.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Index));
        }

        TempData["SuccessMessage"] = $"User {user.Email} has been deleted.";
        return RedirectToAction(nameof(Index));
    }
}
