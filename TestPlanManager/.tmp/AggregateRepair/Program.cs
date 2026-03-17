using Microsoft.EntityFrameworkCore;
using TestPlanManager.Data;

var dbPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "TestPlan.db"));
if (!File.Exists(dbPath))
{
	Console.WriteLine($"ERROR: Database not found at {dbPath}");
	return;
}

var options = new DbContextOptionsBuilder<TestPlanContext>()
	.UseSqlite($"Data Source={dbPath}")
	.Options;

using var ctx = new TestPlanContext(options);

var beforeMismatch = await ctx.TestCategories
	.Select(tc => new
	{
		tc.TestCategoryId,
		tc.TotalTest,
		ActualCount = tc.Tests.Count
	})
	.CountAsync(x => x.TotalTest != x.ActualCount);

var categories = await ctx.TestCategories
	.Include(tc => tc.Tests)
	.ToListAsync();

foreach (var category in categories)
{
	category.Recalculate();
}

var changedRows = await ctx.SaveChangesAsync();

var afterMismatch = await ctx.TestCategories
	.Select(tc => new
	{
		tc.TestCategoryId,
		tc.TotalTest,
		ActualCount = tc.Tests.Count
	})
	.CountAsync(x => x.TotalTest != x.ActualCount);

Console.WriteLine($"Database: {dbPath}");
Console.WriteLine($"Categories checked: {categories.Count}");
Console.WriteLine($"TotalTest mismatches before: {beforeMismatch}");
Console.WriteLine($"Rows changed and saved: {changedRows}");
Console.WriteLine($"TotalTest mismatches after: {afterMismatch}");

var targetBuild = await ctx.Sprints
	.Where(s => s.BuildNr == "Mystore-01")
	.Select(s => new
	{
		s.BuildNr,
		SumCategoryTotal = s.TestCategories.Sum(tc => tc.TotalTest),
		ActualTests = s.TestCategories.SelectMany(tc => tc.Tests).Count(),
		InScope = s.TestCategories.SelectMany(tc => tc.Tests).Count(t => t.ScopeStatus == TestPlanManager.Models.ScopeStatus.InScope),
		OutOfScope = s.TestCategories.SelectMany(tc => tc.Tests).Count(t => t.ScopeStatus == TestPlanManager.Models.ScopeStatus.OutOfScope)
	})
	.FirstOrDefaultAsync();

if (targetBuild != null)
{
	Console.WriteLine($"{targetBuild.BuildNr}: total={targetBuild.SumCategoryTotal}, actual={targetBuild.ActualTests}, inScope={targetBuild.InScope}, outOfScope={targetBuild.OutOfScope}");
}
