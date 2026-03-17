using Microsoft.EntityFrameworkCore;
using TestPlanManager.Data;
using TestPlanManager.Models;

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

var sesameBuilds = await ctx.Sprints
	.Where(s => EF.Functions.Like(s.BuildNr, "Sesame%") || EF.Functions.Like(s.BuildNr, "sesame%"))
	.OrderBy(s => s.BuildNr)
	.Select(s => new
	{
		s.BuildNr,
		SumCategoryTotal = s.TestCategories.Sum(tc => tc.TotalTest),
		ActualTests = s.TestCategories.SelectMany(tc => tc.Tests).Count(),
		InScope = s.TestCategories.SelectMany(tc => tc.Tests).Count(t => t.ScopeStatus == ScopeStatus.InScope),
		OutOfScope = s.TestCategories.SelectMany(tc => tc.Tests).Count(t => t.ScopeStatus == ScopeStatus.OutOfScope),
		Passed = s.TestCategories.SelectMany(tc => tc.Tests).Count(t => t.ExecutionStatus == ExecutionStatus.Passed),
		Failed = s.TestCategories.SelectMany(tc => tc.Tests).Count(t => t.ExecutionStatus == ExecutionStatus.Failed),
		Blocked = s.TestCategories.SelectMany(tc => tc.Tests).Count(t => t.ExecutionStatus == ExecutionStatus.Blocked),
		NotRun = s.TestCategories.SelectMany(tc => tc.Tests).Count(t => t.ExecutionStatus == ExecutionStatus.NotRun)
	})
	.ToListAsync();

Console.WriteLine($"Database: {dbPath}");
if (sesameBuilds.Count == 0)
{
	Console.WriteLine("No builds found matching 'Sesame%'.");
	return;
}

foreach (var b in sesameBuilds)
{
	var ok = b.SumCategoryTotal == b.ActualTests ? "OK" : "MISMATCH";
	Console.WriteLine($"{b.BuildNr} => {ok} | total={b.SumCategoryTotal}, actual={b.ActualTests}, inScope={b.InScope}, outOfScope={b.OutOfScope}, passed={b.Passed}, failed={b.Failed}, blocked={b.Blocked}, notRun={b.NotRun}");
}
