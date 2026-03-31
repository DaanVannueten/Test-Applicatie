using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TestPlanManager.Models;

namespace TestPlanManager.Data
{
    /// <summary>
    /// ============================================================
    /// DATABASE CONTEXT - Entity Framework Core DbContext
    /// ============================================================
    /// Represents the database connection and models for the application.
    /// Inherits from IdentityDbContext to include default Identity tables for users/roles.
    /// 
    /// Data Sets (Tables):
    /// - Sprints: Test cycles (versions) with build numbers
    /// - TestCategories: Groupings of tests within a sprint
    /// - Tests: Individual test cases
    /// - TestTemplates: Reusable test templates
    /// - TemplateTestCases: Test cases within a template
    /// - Identity tables: ApplicationUser, Roles, UserRoles, etc. (inherited)
    /// 
    /// Key Features:
    /// - Cascade deletes for maintaining data integrity
    /// - Foreign key relationships between entities
    /// - Enum-to-string conversions for database readability
    /// ============================================================
    /// </summary>
    public class TestPlanContext : IdentityDbContext<ApplicationUser>
    {
        public TestPlanContext(DbContextOptions<TestPlanContext> options)
            : base(options)
        {
        }

        // DbSet properties represent tables in the database
        /// <summary>
        /// Sprints table: Test cycles/versions (e.g., v1.0, v1.1, v2.0)
        /// Each sprint contains multiple test categories with test cases
        /// </summary>
        public DbSet<Sprint> Sprints { get; set; }

        /// <summary>
        /// TestCategories table: Logical groupings of tests within a sprint
        /// Each category belongs to one sprint
        /// Examples: Functional Tests, UI Tests, Integration Tests, etc.
        /// </summary>
        public DbSet<TestCategory> TestCategories { get; set; }

        /// <summary>
        /// Tests table: Individual test cases
        /// Each test belongs to exactly one category
        /// Stores test name, description, status, and execution results
        /// </summary>
        public DbSet<Test> Tests { get; set; }

        /// <summary>
        /// TestTemplates table: Reusable templates for creating categories
        /// Allows testing different functionality with the same test cases
        /// Can be marked as templates to be used as blueprints
        /// </summary>
        public DbSet<TestTemplate> TestTemplates { get; set; }

        /// <summary>
        /// TemplateTestCases table: Test case definitions within a template
        /// Similar to Tests but used for template purposes
        /// </summary>
        public DbSet<TemplateTestCase> TemplateTestCases { get; set; }

        /// <summary>
        /// Configures the database model relationships, constraints, and conversions.
        /// Called by Entity Framework when initializing the model.
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // RELATIONSHIP: Sprint -> TestCategories (One-to-Many)
            modelBuilder.Entity<Sprint>()
                .HasMany(s => s.TestCategories)       // A sprint has many test categories
                .WithOne(tc => tc.Sprint)             // Each category belongs to one sprint
                .HasForeignKey(tc => tc.SprintId)     // Foreign key in TestCategory table
                .OnDelete(DeleteBehavior.Cascade);    // Delete all categories when sprint is deleted

            // RELATIONSHIP: Sprint -> TestTemplate (Many-to-One, optional)
            modelBuilder.Entity<Sprint>()
                .HasOne(s => s.TestTemplate)          // Sprint references one template
                .WithMany(t => t.Sprints)             // Template can have many sprints
                .HasForeignKey(s => s.TestTemplateId)
                .OnDelete(DeleteBehavior.SetNull);    // Don't delete template if sprint is deleted

            // RELATIONSHIP: TestCategory -> Tests (One-to-Many)
            modelBuilder.Entity<TestCategory>()
                .HasMany(tc => tc.Tests)              // A category has many tests
                .WithOne(t => t.TestCategory)         // Each test belongs to one category
                .HasForeignKey(t => t.TestCategoryId)
                .OnDelete(DeleteBehavior.Cascade);    // Delete all tests when category is deleted

            // RELATIONSHIP: TestCategory -> TestTemplate (Many-to-One, optional)
            modelBuilder.Entity<TestCategory>()
                .HasOne(tc => tc.TestTemplate)        // Category can reference a template
                .WithMany()                           // Template doesn't track this relationship
                .HasForeignKey(tc => tc.TestTemplateId)
                .OnDelete(DeleteBehavior.SetNull);    // Don't delete template if category is deleted

            // RELATIONSHIP: TestTemplate -> TemplateTestCases (One-to-Many)
            modelBuilder.Entity<TestTemplate>()
                .HasMany(t => t.TestCases)            // A template has many test cases
                .WithOne(tc => tc.TestTemplate)       // Each test case belongs to one template
                .HasForeignKey(tc => tc.TestTemplateId)
                .OnDelete(DeleteBehavior.Cascade);    // Delete all test cases when template is deleted

            // INDEX: TemplateTestCase order tracking for templates
            modelBuilder.Entity<TemplateTestCase>()
                .HasIndex(tc => new { tc.TestTemplateId, tc.Sequence });  // Index for fast lookup by template and sequence

            // ENUM CONVERSIONS: Store enums as strings for readability in database
            // Instead of storing enum values as numbers (0, 1, 2), store meaningful names ("InScope", "OutOfScope")

            modelBuilder
                .Entity<TestCategory>()
                .Property(tc => tc.Department)
                .HasConversion<string>();  // Department enum stored as string

            modelBuilder
                .Entity<Test>()
                .Property(t => t.ScopeStatus)
                .HasConversion<string>();  // ScopeStatus enum (InScope/OutOfScope)

            modelBuilder
                .Entity<Test>()
                .Property(t => t.ExecutionStatus)
                .HasConversion<string>();  // ExecutionStatus enum (Passed/Failed/Blocked/NotRun)

            modelBuilder
                .Entity<Test>()
                .Property(t => t.Production)
                .HasConversion<string>();  // Production enum

            modelBuilder
                .Entity<TemplateTestCase>()
                .Property(t => t.ScopeStatus)
                .HasConversion<string>();  // Template scope status

            modelBuilder
                .Entity<TemplateTestCase>()
                .Property(t => t.DefaultExecutionStatus)
                .HasConversion<string>();  // Template default execution status

            // Call base implementation to configure Identity tables
            base.OnModelCreating(modelBuilder);
        }
    }
}