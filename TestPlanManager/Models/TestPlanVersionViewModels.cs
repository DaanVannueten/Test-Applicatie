using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TestPlanManager.Models
{
    public class CreateVersionInputModel
    {
        [Range(1, int.MaxValue, ErrorMessage = "Build number must be greater than 0.")]
        public int BuildNr { get; set; }
    }

    public class CopyVersionInputModel
    {
        [Range(1, int.MaxValue, ErrorMessage = "Build number must be greater than 0.")]
        public int BuildNr { get; set; }

        [Required(ErrorMessage = "Select a source version to copy.")]
        public int? CopyFromSprintId { get; set; }
    }

    public class TestPlanVersionPageViewModel
    {
        public IEnumerable<TestPlanVersionDto> Versions { get; set; } = new List<TestPlanVersionDto>();
        public int? DefaultSprintId { get; set; }
        public CreateVersionInputModel CreateForm { get; set; } = new();
        public CopyVersionInputModel CopyForm { get; set; } = new();
    }
}