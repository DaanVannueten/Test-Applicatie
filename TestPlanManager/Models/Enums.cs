namespace TestPlanManager.Models
{
    public enum Department
    {
        IT,
        Finance,
        Digital,
        Ops
    }

    public enum ScopeStatus
    {
        InScope,
        OutOfScope
    }

    public enum ExecutionStatus
    {
        NotRun,
        Passed,
        Failed,
        Blocked
    }

    public enum ProductionStatus
    {
        NotRun,
        Passed,
        Failed,
        Blocked
    }
}