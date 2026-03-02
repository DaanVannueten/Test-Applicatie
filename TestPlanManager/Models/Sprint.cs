using System.Collections.Generic;

namespace TestPlanManager.Models
{
    public class Sprint
    {
        public int SprintId { get; set; }
        public int BuildNr { get; set; }

        // navigation
        public ICollection<TestCategory> TestCategories { get; set; } = new List<TestCategory>();
    }
}