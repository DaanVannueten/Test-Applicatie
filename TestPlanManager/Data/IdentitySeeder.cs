using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using TestPlanManager.Models;

namespace TestPlanManager.Data;

/// <summary>
/// ============================================================
/// IDENTITY SEEDER - Database Initialization for Users & Roles
/// ============================================================
/// Initializes the database with default roles and admin user on application startup.
/// Called from Program.cs during application initialization.
/// 
/// Responsibilities:
/// - Create application roles (Administrator, TestManager, Tester)
/// - Create or update the default admin user from configuration
/// - Set user properties (IsActive status, CreatedAtUtc timestamp)
/// - Assign roles to users
/// 
/// Configuration Keys (from appsettings.json):
/// - SeedAdmin:Email - Email/username of default admin user
/// - SeedAdmin:Password - Password for default admin user
/// ============================================================
/// </summary>
public static class IdentitySeeder
{
    /// <summary>
    /// Seeds the database with default roles and admin user.
    /// Called once during application startup.
    /// 
    /// Parameters:
    /// - services: IServiceProvider with registered Identity services
    /// 
    /// Process:
    /// 1. Create all application roles if they don't exist
    /// 2. Create or update the default admin user from configuration
    /// 3. Ensure the admin user has Administrator role
    /// </summary>
    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var configuration = services.GetRequiredService<IConfiguration>();

        // STEP 1: Create default application roles if they don't exist
        foreach (var role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                // Create the role in the database
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // STEP 2: Load admin credentials from configuration
        var adminEmail = configuration["SeedAdmin:Email"];
        var adminPassword = configuration["SeedAdmin:Password"];

        // Skip seeding admin if not configured (empty/disabled in appsettings.json)
        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        {
            return;
        }

        // STEP 3: Find existing admin user or create new one
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            // Create new admin user
            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                IsActive = true  // Admin user is always active
            };

            var createResult = await userManager.CreateAsync(adminUser, adminPassword);
            if (!createResult.Succeeded)
            {
                // If user creation fails, throw exception to prevent silent failure
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to create default admin user: {errors}");
            }
        }
        else
        {
            // Admin user exists - ensure it's properly configured
            var needsUpdate = false;

            // Ensure admin is marked as active
            if (!adminUser.IsActive)
            {
                adminUser.IsActive = true;
                needsUpdate = true;
            }

            // Ensure admin has CreatedAtUtc timestamp
            if (adminUser.CreatedAtUtc == default)
            {
                adminUser.CreatedAtUtc = DateTime.UtcNow;
                needsUpdate = true;
            }

            // Save updates if any changes were made
            if (needsUpdate)
            {
                await userManager.UpdateAsync(adminUser);
            }
        }

        // STEP 4: Ensure admin user has Administrator role
        if (!await userManager.IsInRoleAsync(adminUser, AppRoles.Administrator))
        {
            // Add Administrator role to the user
            await userManager.AddToRoleAsync(adminUser, AppRoles.Administrator);
        }
    }
}
