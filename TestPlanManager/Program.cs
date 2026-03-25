using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Diagnostics;
using TestPlanManager.Data;
using TestPlanManager.Models;

/// <summary>
/// ============================================================
/// APPLICATION STARTUP AND CONFIGURATION
/// ============================================================
/// This is the main entry point for the ASP.NET Core application.
/// It sets up the application dependencies, database, authentication,
/// and request pipeline.
/// ============================================================
/// </summary>

var builder = WebApplication.CreateBuilder(args);

/// <summary>
/// SECTION 1: DATABASE CONFIGURATION
/// Sets up the SQLite database connection and Entity Framework Core DbContext.
/// The connection string can be configured via appsettings.json or defaults 
/// to a local SQLite database in App_Data/TestPlan.db folder.
/// </summary>
var configuredConnectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=App_Data/TestPlan.db";
var effectiveConnectionString = ResolveSqliteConnectionString(configuredConnectionString, builder.Environment.ContentRootPath);

// Register the TestPlanContext with Entity Framework Core using SQLite
builder.Services.AddDbContext<TestPlanManager.Data.TestPlanContext>(options =>
    options.UseSqlite(effectiveConnectionString));

/// <summary>
/// SECTION 2: IDENTITY AND AUTHENTICATION SETUP
/// Configures ASP.NET Core Identity for user management and authentication.
/// Sets up password requirements, email uniqueness, and account lockout policies.
/// </summary>
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        // Password Requirements - enforce strong passwords
        options.Password.RequireDigit = true;                    // Must contain at least one digit (0-9)
        options.Password.RequireLowercase = true;                // Must contain at least one lowercase letter (a-z)
        options.Password.RequireUppercase = true;                // Must contain at least one uppercase letter (A-Z)
        options.Password.RequireNonAlphanumeric = true;           // Must contain at least one special character (!@#$%^&*)
        options.Password.RequiredLength = 8;                     // Minimum 8 characters

        // User Requirements
        options.User.RequireUniqueEmail = true;                  // Each user must have a unique email address

        // Account Lockout Policy - prevent brute force attacks
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);  // Lock out for 15 minutes after failed attempts
        options.Lockout.MaxFailedAccessAttempts = 5;             // Lock out after 5 failed login attempts
        options.Lockout.AllowedForNewUsers = true;               // Apply lockout policy to newly created users
    })
    .AddEntityFrameworkStores<TestPlanContext>()  // Use TestPlanContext to store identity data
    .AddDefaultTokenProviders();                   // Generate tokens for password reset and 2FA

/// <summary>
/// SECTION 3: COOKIE AND SESSION CONFIGURATION
/// Configures the authentication cookie settings for security and session management.
/// </summary>
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";                        // Redirect to login page when authentication fails
    options.AccessDeniedPath = "/Account/AccessDenied";          // Redirect to this page when user lacks authorization
    options.Cookie.HttpOnly = true;                              // Cookie is only accessible via HTTP (prevents JavaScript access)
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;     // Cookie is only sent over HTTPS (secure protocol)
    options.SlidingExpiration = true;                            // Reset expiration time on each request
    options.ExpireTimeSpan = TimeSpan.FromHours(8);              // Session expires after 8 hours of inactivity
});

/// <summary>
/// SECTION 4: AUTHORIZATION POLICY SETUP
/// Sets the default authorization policy to require all users to be authenticated.
/// This means all controllers/actions require login by default unless marked with [AllowAnonymous].
/// </summary>
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()              // All pages require login by default
        .Build();
});

/// <summary>
/// SECTION 5: SERVICE REGISTRATION
/// Registers MVC controllers, views, and custom application services.
/// </summary>
builder.Services.AddControllersWithViews();  // Add MVC support for controllers and views

// Register the default version store service as a singleton
// This service persists the user's selected sprint/version preference to disk
builder.Services.AddSingleton<TestPlanManager.Data.IDefaultVersionStore>(sp =>
{
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    return new TestPlanManager.Data.FileDefaultVersionStore(env.ContentRootPath);
});

var app = builder.Build();

/// <summary>
/// SECTION 6: DATABASE INITIALIZATION AND SEEDING
/// Runs database migrations and initializes default data on application startup.
/// This ensures the database schema is up-to-date and has the required seed data.
/// </summary>
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var ctx = services.GetRequiredService<TestPlanManager.Data.TestPlanContext>();
    
    // Apply any pending database migrations to ensure database schema is current
    await ctx.Database.MigrateAsync();
    
    var tests = await ctx.Tests.ToListAsync();
    var hasChanges = false;

    // Clean up test names by removing HTML-like tags and special characters
    foreach (var test in tests)
    {
        var cleanedName = TestPlanManager.Models.TestTitleSanitizer.Clean(test.Name);
        if (cleanedName != test.Name)
        {
            test.Name = cleanedName;
            hasChanges = true;
        }
    }

    // Save any cleaned test names to the database
    if (hasChanges)
    {
        await ctx.SaveChangesAsync();
    }

    // Seed default roles and admin user if configured in appsettings
    await IdentitySeeder.SeedAsync(services);
}

/// <summary>
/// SECTION 7: CLI COMMAND HANDLER
/// Processes command-line arguments for administrative tasks that run before the web server starts.
/// Available commands: --list-users, --reset-password, --ensure-user, --db-stats
/// Only available in Development environment for security.
/// </summary>
if (args.Length > 0)
{
    // Only allow CLI commands in Development to prevent unauthorized administrative access
    if (!builder.Environment.IsDevelopment())
    {
        Console.WriteLine("CLI user-management commands are only available in Development.");
        return;
    }

    var command = args[0];

    /// <summary>
    /// COMMAND: --list-users
    /// Lists all registered users with their roles, lockout status, and failed login attempts.
    /// Format: email | roles: role1,role2 | lockout: date or none | failed: count
    /// </summary>
    if (string.Equals(command, "--list-users", StringComparison.OrdinalIgnoreCase))
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        var users = await userManager.Users.OrderBy(u => u.Email).ToListAsync();
        Console.WriteLine($"Users: {users.Count}");

        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            var lockout = user.LockoutEnd?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture) ?? "none";
            Console.WriteLine($"- {user.Email} | roles: {string.Join(",", roles)} | lockout: {lockout} | failed: {user.AccessFailedCount}");
        }

        return;
    }

    /// <summary>
    /// COMMAND: --reset-password
    /// Resets a user's password and clears any account lockout.
    /// Usage: dotnet run -- --reset-password <email> <newPassword>
    /// </summary>
    if (string.Equals(command, "--reset-password", StringComparison.OrdinalIgnoreCase))
    {
        if (args.Length < 3)
        {
            Console.WriteLine("Usage: dotnet run -- --reset-password <email> <newPassword>");
            return;
        }

        var email = args[1];
        var newPassword = args[2];

        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            Console.WriteLine($"User not found: {email}");
            return;
        }

        // Generate a token to allow password reset without knowing the current password
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, newPassword);

        if (!result.Succeeded)
        {
            Console.WriteLine("Password reset failed:");
            foreach (var error in result.Errors)
            {
                Console.WriteLine($"- {error.Description}");
            }
            return;
        }

        // Clear any account lockout to allow login
        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);
        Console.WriteLine($"Password reset succeeded for {email}");
        return;
    }

    /// <summary>
    /// COMMAND: --ensure-user
    /// Creates a user with specified role if they don't exist, or updates existing user.
    /// Usage: dotnet run -- --ensure-user <email> <password> <role>
    /// Valid roles: Administrator, TestManager, Tester (see AppRoles.All)
    /// </summary>
    if (string.Equals(command, "--ensure-user", StringComparison.OrdinalIgnoreCase))
    {
        if (args.Length < 4)
        {
            Console.WriteLine("Usage: dotnet run -- --ensure-user <email> <password> <role>");
            Console.WriteLine($"Valid roles: {string.Join(", ", AppRoles.All)}");
            return;
        }

        var email = args[1];
        var password = args[2];
        var role = args[3]?.Trim() ?? string.Empty;

        // Validate that the specified role is valid
        if (!AppRoles.All.Contains(role, StringComparer.Ordinal))
        {
            Console.WriteLine($"Invalid role: {role}");
            Console.WriteLine($"Valid roles: {string.Join(", ", AppRoles.All)}");
            return;
        }

        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            // Create new user if they don't exist
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                Console.WriteLine("User create failed:");
                foreach (var error in createResult.Errors)
                {
                    Console.WriteLine($"- {error.Description}");
                }
                return;
            }
        }
        else
        {
            // Update password for existing user
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var resetResult = await userManager.ResetPasswordAsync(user, token, password);
            if (!resetResult.Succeeded)
            {
                Console.WriteLine("Password update failed:");
                foreach (var error in resetResult.Errors)
                {
                    Console.WriteLine($"- {error.Description}");
                }
                return;
            }
        }

        // Clear lockout to enable user
        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);

        // Update user role - remove old roles and add the new role
        var existingRoles = await userManager.GetRolesAsync(user);
        var rolesToRemove = existingRoles
            .Where(r => !string.Equals(r, role, StringComparison.Ordinal))
            .ToArray();

        if (rolesToRemove.Length > 0)
        {
            await userManager.RemoveFromRolesAsync(user, rolesToRemove);
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            await userManager.AddToRoleAsync(user, role);
        }

        Console.WriteLine($"Ensured user: {email} (role: {role})");
        return;
    }

    /// <summary>
    /// COMMAND: --db-stats
    /// Displays database statistics: counts of sprints, test categories, tests, and users
    /// Useful for verifying database connectivity and data presence
    /// </summary>
    if (string.Equals(command, "--db-stats", StringComparison.OrdinalIgnoreCase))
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var ctx = services.GetRequiredService<TestPlanContext>();

        var sprintCount = await ctx.Sprints.CountAsync();
        var categoryCount = await ctx.TestCategories.CountAsync();
        var testCount = await ctx.Tests.CountAsync();

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var userCount = await userManager.Users.CountAsync();

        Console.WriteLine($"Connection: {effectiveConnectionString}");
        Console.WriteLine($"Sprints: {sprintCount}");
        Console.WriteLine($"Categories: {categoryCount}");
        Console.WriteLine($"Tests: {testCount}");
        Console.WriteLine($"Users: {userCount}");
        return;
    }
}

/// <summary>
/// SECTION 8: HTTP REQUEST PIPELINE / MIDDLEWARE CONFIGURATION
/// Configures the middleware pipeline that processes every HTTP request.
/// Middleware is processed in the order it's added here.
/// </summary>

// Exception handling - catch unhandled exceptions and display error page in production
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // HSTS (HTTP Strict Transport Security) - tells browsers to always use HTTPS
    // Maximum age is 30 days by default
    app.UseHsts();
}

// Redirect HTTP requests to HTTPS in production for security
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Enable routing to match URLs to controller actions
app.UseRouting();

// Serve static files (CSS, JavaScript, images) from wwwroot folder
app.UseStaticFiles();

// Authentication middleware - identifies the current user from the auth cookie
app.UseAuthentication();

// Enforce MFA enrollment for authenticated users before granting access to the app.
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var path = context.Request.Path;
        var isAllowedPath =
            path.StartsWithSegments("/Account/EnableAuthenticator", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/Account/DisableAuthenticator", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/Account/RegenerateRecoveryCodes", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/Account/ShowRecoveryCodes", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/Account/Manage", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/Account/Logout", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/Account/AccessDenied", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/Account/LoginWith2fa", StringComparison.OrdinalIgnoreCase);

        if (!isAllowedPath)
        {
            var userManager = context.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.GetUserAsync(context.User);

            if (user != null)
            {
                var twoFactorEnabled = await userManager.GetTwoFactorEnabledAsync(user);
                if (!twoFactorEnabled)
                {
                    context.Response.Redirect("/Account/EnableAuthenticator");
                    return;
                }
            }
        }
    }

    await next();
});

// Authorization middleware - enforces [Authorize] and role-based access control attributes
app.UseAuthorization();

/// <summary>
/// SECTION 9: ROUTE CONFIGURATION
/// Maps controllers to URL patterns for cleaner, more SEO-friendly URLs.
/// Routes are checked in the order they're defined.
/// </summary>

// Edit Test - handles URLs like: test-categories/5/edit
app.MapControllerRoute(
    name: "test-edit",
    pattern: "test-categories/{id:int}/edit",
    defaults: new { controller = "TestCategoryMvc", action = "EditTest" });

// Create New Test - handles URLs like: test-categories/5/tests/new
app.MapControllerRoute(
    name: "test-create",
    pattern: "test-categories/{testCategoryId:int}/tests/new",
    defaults: new { controller = "TestCategoryMvc", action = "CreateTest" });

// Test Category Details - handles URLs like: test-categories/5
app.MapControllerRoute(
    name: "test-category-details",
    pattern: "test-categories/{id:int}",
    defaults: new { controller = "TestCategoryMvc", action = "Details" });

// Default Route - fallback pattern for all other requests
// Default page: TestPlanVersion controller, Index action
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=TestPlanVersion}/{action=Index}/{id?}");

/// <summary>
/// SECTION 10: APPLICATION STARTUP EVENT HANDLER
/// Runs when the application successfully starts listening for requests.
/// Logs the URL and optionally opens browser in Development mode.
/// </summary>
app.Lifetime.ApplicationStarted.Register(() =>
{
    var urls = app.Urls.ToArray();
    if (urls.Length == 0)
    {
        return;
    }

    // Log the accessible URL(s) to console
    app.Logger.LogInformation("Application URL(s): {Urls}", string.Join(", ", urls));
    Console.WriteLine($"Open in browser: {urls[0]}");

    // Don't auto-open browser in production
    if (!app.Environment.IsDevelopment())
    {
        return;
    }

    try
    {
        // Auto-open the browser in Development mode for convenience
        Process.Start(new ProcessStartInfo
        {
            FileName = urls[0],
            UseShellExecute = true
        });
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Could not open browser automatically.");
    }
});

/// <summary>
/// SECTION 11: APPLICATION STARTUP AND LISTENING
/// Configures where the application listens based on environment variables or defaults.
/// Supports custom URL from APP_URL or PORT environment variables.
/// </summary>
var appUrl = Environment.GetEnvironmentVariable("APP_URL");
var port = Environment.GetEnvironmentVariable("PORT");

// Use custom APP_URL if provided (e.g., for containerized deployments)
if (!string.IsNullOrWhiteSpace(appUrl))
{
    app.Run(appUrl);
}
// Use PORT environment variable if provided (e.g., for cloud deployments)
else if (!string.IsNullOrWhiteSpace(port))
{
    app.Run($"http://0.0.0.0:{port}");
}
// Use default configuration from appsettings.json
else
{
    app.Run();
}

/// <summary>
/// SECTION 12: DATABASE CONNECTION STRING HELPER METHOD
/// Resolves SQLite connection strings with relative paths to absolute paths.
/// Ensures database files are created in the correct location even with relative paths.
/// </summary>
/// <param name="configuredConnectionString">Connection string from configuration</param>
/// <param name="contentRootPath">Application content root directory</param>
/// <returns>Resolved connection string with absolute path</returns>
static string ResolveSqliteConnectionString(string configuredConnectionString, string contentRootPath)
{
    var builder = new SqliteConnectionStringBuilder(configuredConnectionString);

    // If the data source is blank, :memory:, or already an absolute path, use as-is
    if (string.IsNullOrWhiteSpace(builder.DataSource) ||
        string.Equals(builder.DataSource, ":memory:", StringComparison.OrdinalIgnoreCase) ||
        Path.IsPathRooted(builder.DataSource))
    {
        return builder.ToString();
    }

    // Convert relative path to absolute path based on content root
    var resolvedPath = Path.GetFullPath(Path.Combine(contentRootPath, builder.DataSource));
    var directory = Path.GetDirectoryName(resolvedPath);

    // Create the directory if it doesn't exist (ensures App_Data folder is created)
    if (!string.IsNullOrWhiteSpace(directory))
    {
        Directory.CreateDirectory(directory);
    }

    // Update connection string with absolute path
    builder.DataSource = resolvedPath;
    return builder.ToString();
}