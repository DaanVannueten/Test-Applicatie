using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TestPlanManager.Models;

namespace TestPlanManager.Data
{
    public class TestPlanContext : IdentityDbContext<ApplicationUser>
    {
        public TestPlanContext(DbContextOptions<TestPlanContext> options)
            : base(options)
        {
        }

        public DbSet<Sprint> Sprints { get; set; }
        public DbSet<TestCategory> TestCategories { get; set; }
        public DbSet<Test> Tests { get; set; }
        public DbSet<TestTemplate> TestTemplates { get; set; }
        public DbSet<TemplateTestCase> TemplateTestCases { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Sprint>()
                .HasMany(s => s.TestCategories)
                .WithOne(tc => tc.Sprint)
                .HasForeignKey(tc => tc.SprintId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Sprint>()
                .HasOne(s => s.TestTemplate)
                .WithMany(t => t.Sprints)
                .HasForeignKey(s => s.TestTemplateId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<TestCategory>()
                .HasMany(tc => tc.Tests)
                .WithOne(t => t.TestCategory)
                .HasForeignKey(t => t.TestCategoryId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TestCategory>()
                .HasOne(tc => tc.TestTemplate)
                .WithMany()
                .HasForeignKey(tc => tc.TestTemplateId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<TestTemplate>()
                .HasMany(t => t.TestCases)
                .WithOne(tc => tc.TestTemplate)
                .HasForeignKey(tc => tc.TestTemplateId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TemplateTestCase>()
                .HasIndex(tc => new { tc.TestTemplateId, tc.Sequence });

            // enum mapping to strings for readability
            modelBuilder
                .Entity<TestCategory>()
                .Property(tc => tc.Department)
                .HasConversion<string>();

            modelBuilder
                .Entity<Test>()
                .Property(t => t.ScopeStatus)
                .HasConversion<string>();
            modelBuilder
                .Entity<Test>()
                .Property(t => t.ExecutionStatus)
                .HasConversion<string>();
            modelBuilder
                .Entity<Test>()
                .Property(t => t.Production)
                .HasConversion<string>();

            modelBuilder
                .Entity<TemplateTestCase>()
                .Property(t => t.ScopeStatus)
                .HasConversion<string>();
            modelBuilder
                .Entity<TemplateTestCase>()
                .Property(t => t.DefaultExecutionStatus)
                .HasConversion<string>();

            base.OnModelCreating(modelBuilder);
        }
    }
}