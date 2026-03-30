using Microsoft.EntityFrameworkCore;
using TestPlanManager.Data;

namespace TestPlanManager.Tests.TestInfrastructure;

internal static class TestContextFactory
{
    public static TestPlanContext Create()
    {
        var options = new DbContextOptionsBuilder<TestPlanContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new TestPlanContext(options);
    }
}
