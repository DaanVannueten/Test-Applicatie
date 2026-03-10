using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TestPlanManager.Models
{
    public class Sprint
    {
        public int SprintId { get; set; }
        [Required]
        [MaxLength(50)]
        public string BuildNr { get; set; } = string.Empty;

        public bool IsArchived { get; set; }

        // navigation
        public ICollection<TestCategory> TestCategories { get; set; } = new List<TestCategory>();
    }
}