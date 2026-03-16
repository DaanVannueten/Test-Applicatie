using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Diagnostics;
using TestPlanManager.Data;
using TestPlanManager.Models;

var builder = WebApplication.CreateBuilder(args);

var configuredConnectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=App_Data/TestPlan.db";
var effectiveConnectionString = ResolveSqliteConnectionString(configuredConnectionString, builder.Environment.ContentRootPath);

// configure EF Core DbContext
builder.Services.AddDbContext<TestPlanManager.Data.TestPlanContext>(options =>
    options.UseSqlite(effectiveConnectionString));

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredLength = 8;
        options.User.RequireUniqueEmail = true;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddEntityFrameworkStores<TestPlanContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
});

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<TestPlanManager.Data.IDefaultVersionStore>(sp =>
{
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    return new TestPlanManager.Data.FileDefaultVersionStore(env.ContentRootPath);
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var ctx = services.GetRequiredService<TestPlanManager.Data.TestPlanContext>();
    await ctx.Database.MigrateAsync();
    var tests = await ctx.Tests.ToListAsync();
    var hasChanges = false;

    foreach (var test in tests)
    {
        var cleanedName = TestPlanManager.Models.TestTitleSanitizer.Clean(test.Name);
        if (cleanedName != test.Name)
        {
            test.Name = cleanedName;
            hasChanges = true;
        }
    }

    if (hasChanges)
    {
        await ctx.SaveChangesAsync();
    }

    await IdentitySeeder.SeedAsync(services);
}

if (args.Length > 0)
{
    if (!builder.Environment.IsDevelopment())
    {
        Console.WriteLine("CLI user-management commands are only available in Development.");
        return;
    }

    var command = args[0];

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

        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);
        Console.WriteLine($"Password reset succeeded for {email}");
        return;
    }

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

        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);

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

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseRouting();

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

// Test category routes with clean URLs
app.MapControllerRoute(
    name: "test-edit",
    pattern: "test-categories/{id:int}/edit",
    defaults: new { controller = "TestCategoryMvc", action = "EditTest" });

app.MapControllerRoute(
    name: "test-create",
    pattern: "test-categories/{testCategoryId:int}/tests/new",
    defaults: new { controller = "TestCategoryMvc", action = "CreateTest" });

app.MapControllerRoute(
    name: "test-category-details",
    pattern: "test-categories/{id:int}",
    defaults: new { controller = "TestCategoryMvc", action = "Details" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=TestPlanVersion}/{action=Index}/{id?}");

app.Lifetime.ApplicationStarted.Register(() =>
{
    var urls = app.Urls.ToArray();
    if (urls.Length == 0)
    {
        return;
    }

    app.Logger.LogInformation("Application URL(s): {Urls}", string.Join(", ", urls));
    Console.WriteLine($"Open in browser: {urls[0]}");

    if (!app.Environment.IsDevelopment())
    {
        return;
    }

    try
    {
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

var appUrl = Environment.GetEnvironmentVariable("APP_URL");
var port = Environment.GetEnvironmentVariable("PORT");

if (!string.IsNullOrWhiteSpace(appUrl))
{
    app.Run(appUrl);
}
else if (!string.IsNullOrWhiteSpace(port))
{
    app.Run($"http://0.0.0.0:{port}");
}
else
{
    app.Run();
}

static string ResolveSqliteConnectionString(string configuredConnectionString, string contentRootPath)
{
    var builder = new SqliteConnectionStringBuilder(configuredConnectionString);

    if (string.IsNullOrWhiteSpace(builder.DataSource) ||
        string.Equals(builder.DataSource, ":memory:", StringComparison.OrdinalIgnoreCase) ||
        Path.IsPathRooted(builder.DataSource))
    {
        return builder.ToString();
    }

    var resolvedPath = Path.GetFullPath(Path.Combine(contentRootPath, builder.DataSource));
    var directory = Path.GetDirectoryName(resolvedPath);

    if (!string.IsNullOrWhiteSpace(directory))
    {
        Directory.CreateDirectory(directory);
    }

    builder.DataSource = resolvedPath;
    return builder.ToString();
}