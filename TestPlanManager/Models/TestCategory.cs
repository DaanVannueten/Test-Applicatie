using System;
using System.Collections.Generic;

namespace TestPlanManager.Models
{
    public class TestCategory
    {
        public int TestCategoryId { get; set; }
        public int SprintId { get; set; }
        public int? TestTemplateId { get; set; }
        public bool IsTemplateCategory { get; set; }
        public string Name { get; set; } = string.Empty;
        public int TotalTest { get; set; }
        public int OutOfScope { get; set; }
        public int Failed { get; set; }
        public int Blocked { get; set; }
        public int Passed { get; set; }
        public float PercentagePassed { get; set; }
        // brief text describing the category
        // nullable so seed data can omit it and existing rows may not have a value
        public string? Description { get; set; }
        public int Sequence { get; set; }
        public string Status => DetermineStatus();
        public DateTime? TestDate { get; set; }
        public Department Department { get; set; }

        // navigation
        public TestTemplate? TestTemplate { get; set; }
        public Sprint Sprint { get; set; } = null!;
        public ICollection<Test> Tests { get; set; } = new List<Test>();

        // helper to recalc aggregates (optional)
        public void Recalculate()
        {
            TotalTest = Tests.Count;
            OutOfScope = Tests.Count(t => t.ScopeStatus == ScopeStatus.OutOfScope);
            Failed = Tests.Count(t => t.ExecutionStatus == ExecutionStatus.Failed);
            Blocked = Tests.Count(t => t.ExecutionStatus == ExecutionStatus.Blocked);
            Passed = Tests.Count(t => t.ExecutionStatus == ExecutionStatus.Passed);

            // Progress only reflects results within scope.
            var inScopeTotal = Tests.Count(t => t.ScopeStatus == ScopeStatus.InScope);
            var passedInScope = Tests.Count(t =>
                t.ScopeStatus == ScopeStatus.InScope &&
                t.ExecutionStatus == ExecutionStatus.Passed);

            PercentagePassed = inScopeTotal == 0 ? 0 : ((float)passedInScope / inScopeTotal) * 100;
        }

        private string DetermineStatus()
        {
            if (PercentagePassed == 0) return "Not started";
            if (PercentagePassed < 100) return "In testing";
            return "Completed";
        }
    }
}