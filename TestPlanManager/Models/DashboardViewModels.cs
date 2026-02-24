using System.Collections.Generic;

namespace TestPlanManager.Models
{
    public class TestCategoryDto
    {
        public int TestCategoryId { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }
        public int BuildNr { get; set; }
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
        public IEnumerable<TestCategoryDto> Categories { get; set; }
    }
}