namespace TestPlanManager.Models;

public class TemplateTestCase
{
    public int TemplateTestCaseId { get; set; }
    public int TestTemplateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? MediaUrl { get; set; }
    public ScopeStatus ScopeStatus { get; set; } = ScopeStatus.InScope;
    public ExecutionStatus DefaultExecutionStatus { get; set; } = ExecutionStatus.NotRun;
    public string Production { get; set; } = string.Empty;
    public int Sequence { get; set; }

    public TestTemplate TestTemplate { get; set; } = null!;
}
