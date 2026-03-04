using System.ComponentModel.DataAnnotations;

namespace TestPlanManager.Models
{
    public class EditCategoryInputModel
    {
        [Required]
        public int TestCategoryId { get; set; }

        [Required]
        public int SprintId { get; set; }

        [Required(ErrorMessage = "Naam is verplicht.")]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required(ErrorMessage = "Department is verplicht.")]
        public Department Department { get; set; } = Department.IT;

        [Range(1, int.MaxValue, ErrorMessage = "Order must be at least 1.")]
        public int Sequence { get; set; } = 1;

        public int BuildNr { get; set; }

        public string? ReturnUrl { get; set; }
    }
}
