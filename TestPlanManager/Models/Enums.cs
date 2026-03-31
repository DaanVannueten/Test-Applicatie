namespace TestPlanManager.Models
{
    /// <summary>
    /// ============================================================
    /// ENUMERATION DEFINITIONS - Application Constants
    /// ============================================================
    /// Defines application domain enumerations used throughout the system.
    /// These are converted to strings in the database for readability.
    /// ============================================================
    /// </summary>

    /// <summary>
    /// Department enumeration - organizational divisions.
    /// Used to categorize test categories by department.
    /// </summary>
    public enum Department
    {
        /// <summary>Information Technology department</summary>
        IT,

        /// <summary>Finance department</summary>
        Finance,

        /// <summary>Digital/Web division</summary>
        Digital,

        /// <summary>Operations department</summary>
        Ops
    }

    /// <summary>
    /// Scope Status enumeration - indicates test scope for a sprint.
    /// Determines whether a test result counts toward pass rates.
    /// 
    /// Impact on Metrics:
    /// - InScope tests: Included in pass rate and status calculations
    /// - OutOfScope tests: Excluded from pass rate calculations
    /// 
    /// Use Cases:
    /// - Features deferred to later sprints marked OutOfScope
    /// - Defects postponed marked OutOfScope
    /// - Breaking changes marked OutOfScope for certain clients
    /// </summary>
    public enum ScopeStatus
    {
        /// <summary>Test is required to pass for this sprint</summary>
        InScope,

        /// <summary>Test is not required for this sprint (excluded from pass rate)</summary>
        OutOfScope
    }

    /// <summary>
    /// Execution Status enumeration - tracks test execution result.
    /// Updated when tester executes a test and records the outcome.
    /// 
    /// State Transitions:
    /// - NotRun -> Passed/Failed/Blocked (test executed for first time)
    /// - Passed/Failed/Blocked -> NotRun (test reset/retested)
    /// - Any -> Any (test re-execution with different result)
    /// </summary>
    public enum ExecutionStatus
    {
        /// <summary>Test has not been executed yet</summary>
        NotRun,

        /// <summary>Test executed and passed (all acceptance criteria met)</summary>
        Passed,

        /// <summary>Test executed and failed (acceptance criteria not met)</summary>
        Failed,

        /// <summary>Test execution blocked (cannot execute due to external issue/dependency)</summary>
        Blocked
    }

    /// <summary>
    /// Production Status enumeration - tracks production environment status.
    /// Mirrors ExecutionStatus with same values.
    /// Used to track whether functionality is working in production.
    /// 
    /// Note: Currently mirrors ExecutionStatus enum.
    /// Could be extended for production-specific statuses if needed.
    /// </summary>
    public enum ProductionStatus
    {
        /// <summary>Not verified in production</summary>
        NotRun,

        /// <summary>Working correctly in production</summary>
        Passed,

        /// <summary>Issue found in production</summary>
        Failed,

        /// <summary>Cannot verify in production (blocked)</summary>
        Blocked
    }
}