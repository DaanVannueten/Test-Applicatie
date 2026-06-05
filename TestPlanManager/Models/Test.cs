namespace TestPlanManager.Models
{
    /// <summary>
    /// ============================================================
    /// TEST MODEL - Individual Test Case
    /// ============================================================
    /// Represents a single test case within a test category.
    /// Stores test execution status, scope, and related metadata.
    /// 
    /// Properties:
    /// - TestId: Unique identifier
    /// - TestCategoryId: Foreign key to parent category
    /// - Name: Test case name/description
    /// - ScopeStatus: Whether test is in scope or out of scope for this sprint
    /// - ExecutionStatus: Passed/Failed/Blocked/NotRun
    /// - Description: Detailed test description
    /// - Dependencies: Related prerequisite or dependency notes
    /// - Comments: Execution notes/observations
    /// - MediaUrl: URL to screenshot or video evidence
    /// - Production: Production environment version/build info
    /// - ExecutedAt: Timestamp when test was executed
    /// - LastExecutedBy: Username of who executed the test
    /// ============================================================
    /// </summary>
    public class Test
    {
        /// <summary>
        /// Primary key - unique identifier for this test case
        /// </summary>
        public int TestId { get; set; }

        /// <summary>
        /// Foreign key - reference to parent TestCategory
        /// Each test belongs to exactly one category
        /// </summary>
        public int TestCategoryId { get; set; }

        /// <summary>
        /// Optional reference to the source template test case ID
        /// Indicates if this test was derived from a template
        /// </summary>
        public int? TemplateTestCaseId { get; set; }

        /// <summary>
        /// Flag indicating if this test was created from a template
        /// Used to track inherited test properties
        /// </summary>
        public bool IsTemplateDerived { get; set; }

        /// <summary>
        /// Test case name/title (e.g., "Login screen validation", "User registration form")
        /// Display name shown in test lists and reports
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Scope status: InScope or OutOfScope
        /// Indicates whether this test is required to pass for this sprint
        /// OutOfScope tests are excluded from pass rate calculations
        /// </summary>
        public ScopeStatus ScopeStatus { get; set; }

        /// <summary>
        /// Execution status: Passed, Failed, Blocked, or NotRun
        /// Current test execution result
        /// </summary>
        public ExecutionStatus ExecutionStatus { get; set; }

        /// <summary>
        /// Detailed test description with steps to reproduce
        /// May include expected results and preconditions
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Related dependency notes or prerequisites
        /// </summary>
        public string Dependencies { get; set; } = string.Empty;

        /// <summary>
        /// Tester's comments during execution
        /// May contain observations, error messages, or notes
        /// </summary>
        public string? Comments { get; set; }

        /// <summary>
        /// URL to evidence of test execution
        /// Can reference screenshot, video, or other media
        /// </summary>
        public string? MediaUrl { get; set; }

        /// <summary>
        /// Production environment version or build number
        /// Context information about where test was executed
        /// Example: "v1.2.3", "Build 456", "Staging"
        /// </summary>
        public string Production { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp when this test was executed
        /// Set to UTC time when test status changes from NotRun
        /// Cleared if test is reverted to NotRun
        /// </summary>
        public DateTime? ExecutedAt { get; set; }

        /// <summary>
        /// Username/email of the person who executed this test
        /// Tracks who performed the test execution
        /// </summary>
        public string? LastExecutedBy { get; set; }

        // Navigation property - relationship to parent category
        /// <summary>
        /// Navigation property - reference to parent TestCategory entity
        /// Used for accessing the category this test belongs to
        /// </summary>
        public TestCategory TestCategory { get; set; } = null!;
    }
}