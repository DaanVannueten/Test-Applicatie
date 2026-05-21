using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TestPlanManager.Models
{
    public class CreateVersionInputModel
    {
        [Required(ErrorMessage = "Build number is required.")]
        [StringLength(50, ErrorMessage = "Build number cannot be longer than 50 characters.")]
        [RegularExpression(@"^[A-Za-z0-9._-]+$", ErrorMessage = "Build number may only contain letters, numbers, periods (.), underscores (_), and hyphens (-); spaces are not allowed.")]
        public string BuildNr { get; set; } = string.Empty;

        public bool IsTemplate { get; set; }
    }

    public class CopyVersionInputModel
    {
        [Required(ErrorMessage = "Build number is required.")]
        [StringLength(50, ErrorMessage = "Build number cannot be longer than 50 characters.")]
        [RegularExpression(@"^[A-Za-z0-9._-]+$", ErrorMessage = "Build number may only contain letters, numbers, dot, underscore, and hyphen.")]
        public string BuildNr { get; set; } = string.Empty;

        [Required(ErrorMessage = "Select a source version to copy.")]
        public int? CopyFromSprintId { get; set; }
    }

    public class CreateTemplateFromCycleInputModel
    {
        [Required(ErrorMessage = "Template build number is required.")]
        [StringLength(50, ErrorMessage = "Build number cannot be longer than 50 characters.")]
        [RegularExpression(@"^[A-Za-z0-9._-]+$", ErrorMessage = "Build number may only contain letters, numbers, dot, underscore, and hyphen.")]
        public string BuildNr { get; set; } = string.Empty;

        [Required(ErrorMessage = "Select a source cycle.")]
        public int? SourceCycleSprintId { get; set; }
    }

    public class TestPlanVersionPageViewModel
    {
        public IEnumerable<TestPlanVersionDto> Versions { get; set; } = new List<TestPlanVersionDto>();
        public int? DefaultSprintId { get; set; }
        public bool ShowArchived { get; set; }
        public CreateVersionInputModel CreateForm { get; set; } = new();
        public CopyVersionInputModel CopyForm { get; set; } = new();
        public CreateTemplateFromCycleInputModel CreateTemplateFromCycleForm { get; set; } = new();
    }
}