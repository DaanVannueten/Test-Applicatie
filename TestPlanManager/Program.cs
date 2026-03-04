using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// configure EF Core DbContext
builder.Services.AddDbContext<TestPlanManager.Data.TestPlanContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

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
    var ctx = scope.ServiceProvider.GetRequiredService<TestPlanManager.Data.TestPlanContext>();
    var tests = ctx.Tests.ToList();
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
        ctx.SaveChanges();
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

app.UseAuthorization();

app.MapStaticAssets();

// Test category routes with clean URLs
app.MapControllerRoute(
    name: "test-edit",
    pattern: "test-categories/{id:int}/edit",
    defaults: new { controller = "TestCategoryMvc", action = "EditTest" })
    .WithStaticAssets();

app.MapControllerRoute(
    name: "test-create",
    pattern: "test-categories/{testCategoryId:int}/tests/new",
    defaults: new { controller = "TestCategoryMvc", action = "CreateTest" })
    .WithStaticAssets();

app.MapControllerRoute(
    name: "test-category-details",
    pattern: "test-categories/{id:int}",
    defaults: new { controller = "TestCategoryMvc", action = "Details" })
    .WithStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

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