namespace TestPlanManager.Models;

public class TestTemplate
{
    public int TestTemplateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<TemplateTestCase> TestCases { get; set; } = new List<TemplateTestCase>();
    public ICollection<Sprint> Sprints { get; set; } = new List<Sprint>();
}
