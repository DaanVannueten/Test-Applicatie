using System.Collections.Generic;

namespace TestPlanManager.Models
{
    public class TestPlanVersionDto
    {
        public int SprintId { get; set; }
        public string BuildNr { get; set; } = string.Empty;
        public bool IsArchived { get; set; }
        public bool IsTemplate { get; set; }
        public int? SourceTemplateSprintId { get; set; }
        public int CategoryCount { get; set; }
        public int TestCount { get; set; }
        public DateTime? LastExecutionDate { get; set; }
        public string? LastWorkedBy { get; set; }
    }

    public class TestCategoryDto
    {
        public int TestCategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string BuildNr { get; set; } = string.Empty;
        public Department Department { get; set; }
        public int Passed { get; set; }
        public int Failed { get; set; }
        public int Blocked { get; set; }
        public int OutOfScope { get; set; }
        public int NotRun => TotalTest - Passed - Failed - Blocked - OutOfScope;
        public int TotalTest { get; set; }
        public float PercentagePassed { get; set; }
        public string Status => DetermineStatus();

        private string DetermineStatus()
        {
            if (PercentagePassed == 0) return "Not started";
            if (PercentagePassed < 100) return "In testing";
            return "Completed";
        }
    }

    public class DashboardViewModel
    {
        public IEnumerable<TestPlanVersionDto> Versions { get; set; } = new List<TestPlanVersionDto>();
        public int? SelectedSprintId { get; set; }
        public string? SelectedBuildNr { get; set; }
        public bool SelectedIsTemplate { get; set; }
        public bool IncludeTemplatesView { get; set; }
        public IEnumerable<TestCategoryDto> Categories { get; set; } = new List<TestCategoryDto>();

        // additional dashboard metrics derived from actual test rows
        public int TotalTestCases { get; set; }
        public int TestsExecuted { get; set; }
        public float OverallPassRate { get; set; }
        public int OpenOrFailed { get; set; }
        public float PassedRate { get; set; }
        public float FailedRate { get; set; }
        public float BlockedRate { get; set; }
        public float OutOfScopeRate { get; set; }
    }
}