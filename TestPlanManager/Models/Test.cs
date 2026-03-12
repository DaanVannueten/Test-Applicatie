namespace TestPlanManager.Models
{
    public class Test
    {
        public int TestId { get; set; }
        public int TestCategoryId { get; set; }
        public int? TemplateTestCaseId { get; set; }
        public bool IsTemplateDerived { get; set; }
        public string Name { get; set; } = string.Empty;
        public ScopeStatus ScopeStatus { get; set; }
        public ExecutionStatus ExecutionStatus { get; set; }
        public string Description { get; set; } = string.Empty;
        public string? Comments { get; set; }
        public string? MediaUrl { get; set; }
        public string Production { get; set; } = string.Empty;
        public DateTime? ExecutedAt { get; set; }
        public string? LastExecutedBy { get; set; }

        // navigation
        public TestCategory TestCategory { get; set; } = null!;
    }
}