using Microsoft.Data.Sqlite;

var dbPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "TestPlan.db"));

if (!File.Exists(dbPath))
{
	Console.WriteLine($"ERROR: Database not found at {dbPath}");
	return;
}

using var connection = new SqliteConnection($"Data Source={dbPath}");
connection.Open();

Console.WriteLine($"Using database: {dbPath}");

var categoryMismatches = QueryInt(connection, @"
SELECT COUNT(*)
FROM (
	SELECT tc.TestCategoryId
	FROM TestCategories tc
	LEFT JOIN Tests t ON t.TestCategoryId = tc.TestCategoryId
	GROUP BY tc.TestCategoryId, tc.TotalTest, tc.OutOfScope, tc.Passed, tc.Failed, tc.Blocked
	HAVING tc.TotalTest <> COUNT(t.TestId)
	   OR tc.OutOfScope <> SUM(CASE WHEN t.ScopeStatus = 'OutOfScope' THEN 1 ELSE 0 END)
	   OR tc.Passed <> SUM(CASE WHEN t.ScopeStatus <> 'OutOfScope' AND t.ExecutionStatus = 'Passed' THEN 1 ELSE 0 END)
	   OR tc.Failed <> SUM(CASE WHEN t.ScopeStatus <> 'OutOfScope' AND t.ExecutionStatus = 'Failed' THEN 1 ELSE 0 END)
	   OR tc.Blocked <> SUM(CASE WHEN t.ScopeStatus <> 'OutOfScope' AND t.ExecutionStatus = 'Blocked' THEN 1 ELSE 0 END)
)
");

Console.WriteLine($"Category mismatches: {categoryMismatches}");

var buildMismatches = QueryInt(connection, @"
SELECT COUNT(*)
FROM (
	SELECT s.SprintId
	FROM Sprints s
	LEFT JOIN TestCategories tc ON tc.SprintId = s.SprintId
	LEFT JOIN Tests t ON t.TestCategoryId = tc.TestCategoryId
	GROUP BY s.SprintId
	HAVING SUM(tc.TotalTest) <> COUNT(t.TestId)
)
");

Console.WriteLine($"Build mismatches (sum category TotalTest vs actual tests): {buildMismatches}");

using (var cmd = connection.CreateCommand())
{
	cmd.CommandText = @"
SELECT s.BuildNr,
	   COALESCE(SUM(tc.TotalTest), 0) AS SumCategoryTotals,
	   COUNT(t.TestId) AS ActualTests
FROM Sprints s
LEFT JOIN TestCategories tc ON tc.SprintId = s.SprintId
LEFT JOIN Tests t ON t.TestCategoryId = tc.TestCategoryId
GROUP BY s.SprintId, s.BuildNr
HAVING COALESCE(SUM(tc.TotalTest), 0) <> COUNT(t.TestId)
ORDER BY s.BuildNr;
";

	using var reader = cmd.ExecuteReader();
	Console.WriteLine("Mismatched builds:");
	while (reader.Read())
	{
		var buildNr = reader.GetString(0);
		var sumCategoryTotals = reader.GetInt32(1);
		var actualTests = reader.GetInt32(2);
		Console.WriteLine($"- {buildNr}: sumCategoryTotal={sumCategoryTotals}, actualTests={actualTests}");
	}
}

using (var cmd = connection.CreateCommand())
{
	cmd.CommandText = @"
SELECT s.BuildNr, tc.Name,
	   tc.TotalTest AS StoredTotal,
	   COUNT(t.TestId) AS ActualTotal
FROM TestCategories tc
JOIN Sprints s ON s.SprintId = tc.SprintId
LEFT JOIN Tests t ON t.TestCategoryId = tc.TestCategoryId
GROUP BY tc.TestCategoryId, s.BuildNr, tc.Name, tc.TotalTest
HAVING tc.TotalTest <> COUNT(t.TestId)
ORDER BY ABS(tc.TotalTest - COUNT(t.TestId)) DESC, s.BuildNr, tc.Name
LIMIT 10;
";

	using var reader = cmd.ExecuteReader();
	Console.WriteLine("Top 10 category total mismatches:");
	while (reader.Read())
	{
		var buildNr = reader.GetString(0);
		var categoryName = reader.GetString(1);
		var stored = reader.GetInt32(2);
		var actual = reader.GetInt32(3);
		Console.WriteLine($"- {buildNr} / {categoryName}: stored={stored}, actual={actual}, delta={stored - actual}");
	}
}

using (var cmd = connection.CreateCommand())
{
	cmd.CommandText = @"
SELECT s.BuildNr,
	   COALESCE(SUM(tc.TotalTest), 0) AS SumCategoryTotals,
	   COUNT(t.TestId) AS ActualTests,
	   SUM(CASE WHEN t.ScopeStatus = 'InScope' THEN 1 ELSE 0 END) AS InScopeTests,
	   SUM(CASE WHEN t.ScopeStatus = 'OutOfScope' THEN 1 ELSE 0 END) AS OutOfScopeTests
FROM Sprints s
LEFT JOIN TestCategories tc ON tc.SprintId = s.SprintId
LEFT JOIN Tests t ON t.TestCategoryId = tc.TestCategoryId
WHERE LOWER(s.BuildNr) LIKE '%mystroy 01%' OR LOWER(s.BuildNr) LIKE '%mystery 01%'
GROUP BY s.SprintId, s.BuildNr
ORDER BY s.BuildNr;
";

	using var reader = cmd.ExecuteReader();
	if (!reader.HasRows)
	{
		Console.WriteLine("No build found matching 'mystroy 01' or 'mystery 01'.");
	}
	else
	{
		Console.WriteLine("Matched builds:");
		while (reader.Read())
		{
			var buildNr = reader.GetString(0);
			var total = reader.GetInt32(1);
			var actual = reader.GetInt32(2);
			var inScope = reader.GetInt32(3);
			var outOfScope = reader.GetInt32(4);
			Console.WriteLine($"- {buildNr}: total={total}, actual={actual}, inScope={inScope}, outOfScope={outOfScope}");
		}
	}

	using (var cmd2 = connection.CreateCommand())
	{
		cmd2.CommandText = @"
	SELECT s.BuildNr,
		   COALESCE(SUM(tc.TotalTest), 0) AS SumCategoryTotals,
		   COUNT(t.TestId) AS ActualTests,
		   SUM(CASE WHEN t.ScopeStatus = 'InScope' THEN 1 ELSE 0 END) AS InScopeTests,
		   SUM(CASE WHEN t.ScopeStatus = 'OutOfScope' THEN 1 ELSE 0 END) AS OutOfScopeTests
	FROM Sprints s
	LEFT JOIN TestCategories tc ON tc.SprintId = s.SprintId
	LEFT JOIN Tests t ON t.TestCategoryId = tc.TestCategoryId
	WHERE LOWER(s.BuildNr) LIKE '%mys%01%'
	GROUP BY s.SprintId, s.BuildNr
	ORDER BY s.BuildNr;
	";

		using var reader2 = cmd2.ExecuteReader();
		Console.WriteLine("Possible build name matches for '%mys%01%':");
		while (reader2.Read())
		{
			var buildNr = reader2.GetString(0);
			var total = reader2.GetInt32(1);
			var actual = reader2.GetInt32(2);
			var inScope = reader2.GetInt32(3);
			var outOfScope = reader2.GetInt32(4);
			Console.WriteLine($"- {buildNr}: total={total}, actual={actual}, inScope={inScope}, outOfScope={outOfScope}");
		}
	}
}

static int QueryInt(SqliteConnection connection, string sql)
{
	using var cmd = connection.CreateCommand();
	cmd.CommandText = sql;
	return Convert.ToInt32(cmd.ExecuteScalar());
}
