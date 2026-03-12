using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using TestPlanManager.Data;
using TestPlanManager.Models;

var builder = WebApplication.CreateBuilder(args);

// configure EF Core DbContext
builder.Services.AddDbContext<TestPlanManager.Data.TestPlanContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

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