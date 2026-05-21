using System.ComponentModel.DataAnnotations;

namespace TestPlanManager.Models;

public class TemplateOverviewViewModel
{
    public int TestTemplateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int TestCaseCount { get; set; }
    public int ActiveCycleCount { get; set; }
}

public class TemplateIndexPageViewModel
{
    public List<TemplateOverviewViewModel> Templates { get; set; } = [];
    public CreateTemplateInputModel CreateTemplate { get; set; } = new();
}

public class TemplateDetailsPageViewModel
{
    public TestTemplate Template { get; set; } = null!;
    public List<TemplateTestCase> TestCases { get; set; } = [];
    public AddTemplateTestCaseInputModel AddTestCase { get; set; } = new();
    public CreateCycleFromTemplateInputModel CreateCycle { get; set; } = new();
}

public class CreateTemplateInputModel
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string ProductName { get; set; } = string.Empty;
}

public class AddTemplateTestCaseInputModel
{
    [Required]
    public int TestTemplateId { get; set; }

    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    public string? MediaUrl { get; set; }

    public ScopeStatus ScopeStatus { get; set; } = ScopeStatus.InScope;

    public ExecutionStatus DefaultExecutionStatus { get; set; } = ExecutionStatus.NotRun;

    public string Production { get; set; } = string.Empty;
}

public class CreateCycleFromTemplateInputModel
{
    [Required]
    public int TestTemplateId { get; set; }

    [Required]
    [StringLength(50)]
    [RegularExpression(@"^[A-Za-z0-9._-]+$", ErrorMessage = "Use only letters, numbers, periods (.), underscores (_), or hyphens (-). Spaces are not allowed.")]
    public string BuildNr { get; set; } = string.Empty;
}
