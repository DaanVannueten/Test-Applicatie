using Microsoft.EntityFrameworkCore;
using TestPlanManager.Models;

namespace TestPlanManager.Data
{
    public class TestPlanContext : DbContext
    {
        public TestPlanContext(DbContextOptions<TestPlanContext> options)
            : base(options)
        {
        }

        public DbSet<Sprint> Sprints { get; set; }
        public DbSet<TestCategory> TestCategories { get; set; }
        public DbSet<Test> Tests { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Sprint>()
                .HasMany(s => s.TestCategories)
                .WithOne(tc => tc.Sprint)
                .HasForeignKey(tc => tc.SprintId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TestCategory>()
                .HasMany(tc => tc.Tests)
                .WithOne(t => t.TestCategory)
                .HasForeignKey(t => t.TestCategoryId)
                .OnDelete(DeleteBehavior.Cascade);

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

            base.OnModelCreating(modelBuilder);
        }
    }
}