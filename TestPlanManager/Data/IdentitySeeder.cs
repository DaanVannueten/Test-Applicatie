using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using TestPlanManager.Models;

namespace TestPlanManager.Data;

public static class IdentitySeeder
{
    private const string FallbackAdminEmail = "admin@testplan.local";
    private const string FallbackAdminPassword = "Admin1234!";

    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var configuration = services.GetRequiredService<IConfiguration>();

        foreach (var role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var adminEmail = configuration["SeedAdmin:Email"] ?? FallbackAdminEmail;
        var adminPassword = configuration["SeedAdmin:Password"] ?? FallbackAdminPassword;

        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(adminUser, adminPassword);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to create default admin user: {errors}");
            }
        }

        if (!await userManager.IsInRoleAsync(adminUser, AppRoles.Administrator))
        {
            await userManager.AddToRoleAsync(adminUser, AppRoles.Administrator);
        }
    }
}
