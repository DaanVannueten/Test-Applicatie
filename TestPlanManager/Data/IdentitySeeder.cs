using Microsoft.AspNetCore.Identity;
using TestPlanManager.Models;

namespace TestPlanManager.Data;

public static class IdentitySeeder
{
    private const string DefaultAdminEmail = "admin@testplan.local";
    private const string DefaultAdminPassword = "Admin1234";

    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        if (!await roleManager.RoleExistsAsync(AppRoles.Admin))
        {
            await roleManager.CreateAsync(new IdentityRole(AppRoles.Admin));
        }

        if (!await roleManager.RoleExistsAsync(AppRoles.User))
        {
            await roleManager.CreateAsync(new IdentityRole(AppRoles.User));
        }

        var adminUser = await userManager.FindByEmailAsync(DefaultAdminEmail);
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = DefaultAdminEmail,
                Email = DefaultAdminEmail,
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(adminUser, DefaultAdminPassword);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to create default admin user: {errors}");
            }
        }

        if (!await userManager.IsInRoleAsync(adminUser, AppRoles.Admin))
        {
            await userManager.AddToRoleAsync(adminUser, AppRoles.Admin);
        }

        if (!await userManager.IsInRoleAsync(adminUser, AppRoles.User))
        {
            await userManager.AddToRoleAsync(adminUser, AppRoles.User);
        }
    }
}
