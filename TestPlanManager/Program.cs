using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// configure EF Core DbContext
builder.Services.AddDbContext<TestPlanManager.Data.TestPlanContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "testcategory",
    pattern: "TestCategory/{action=Details}/{id?}",
    defaults: new { controller = "TestCategoryMvc" })
    .WithStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

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
