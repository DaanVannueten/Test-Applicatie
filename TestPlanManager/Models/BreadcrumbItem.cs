namespace TestPlanManager.Models
{
    public class BreadcrumbItem
    {
        public string Label { get; set; } = string.Empty;
        public string? Url { get; set; }
        public bool IsCurrent { get; set; }
    }
}