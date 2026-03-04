namespace TestPlanManager.Models
{
    public static class TestTitleSanitizer
    {
        public static string Clean(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value.Trim().TrimEnd(',', '\'', '’', ' ');
        }
    }
}
