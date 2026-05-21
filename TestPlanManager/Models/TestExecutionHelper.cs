namespace TestPlanManager.Models
{
    public static class TestExecutionHelper
    {
        public static void ApplyExecution(Test test, ExecutionStatus executionStatus, string? production, string? comments, string? mediaUrl, string? executedBy, DateTime utcNow)
        {
            test.ExecutionStatus = executionStatus;
            test.Production = production ?? string.Empty;
            test.Comments = comments ?? string.Empty;
            test.MediaUrl = mediaUrl ?? string.Empty;

            if (executionStatus == ExecutionStatus.NotRun)
            {
                test.ExecutedAt = null;
                test.LastExecutedBy = null;
                return;
            }

            test.ExecutedAt = utcNow;
            test.LastExecutedBy = executedBy;
        }

        public static DateTime? GetLatestExecutionDate(IEnumerable<Test> tests)
        {
            return tests
                .Where(test => test.ExecutionStatus != ExecutionStatus.NotRun && test.ExecutedAt.HasValue)
                .Select(test => test.ExecutedAt)
                .Max();
        }

        public static void ClearExecution(Test test)
        {
            test.ExecutionStatus = ExecutionStatus.NotRun;
            test.ExecutedAt = null;
            test.LastExecutedBy = null;
        }
    }
}