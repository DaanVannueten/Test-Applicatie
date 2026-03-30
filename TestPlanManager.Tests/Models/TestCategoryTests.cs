using TestPlanManager.Models;

namespace TestPlanManager.Tests.Models;

public class TestCategoryTests
{
    [Fact]
    public void Recalculate_UsesOnlyInScopeTestsForPercentage()
    {
        var category = new TestCategory
        {
            Tests = new List<Test>
            {
                new() { ScopeStatus = ScopeStatus.InScope, ExecutionStatus = ExecutionStatus.Passed },
                new() { ScopeStatus = ScopeStatus.InScope, ExecutionStatus = ExecutionStatus.Failed },
                new() { ScopeStatus = ScopeStatus.OutOfScope, ExecutionStatus = ExecutionStatus.Passed },
                new() { ScopeStatus = ScopeStatus.OutOfScope, ExecutionStatus = ExecutionStatus.Blocked }
            }
        };

        category.Recalculate();

        Assert.Equal(4, category.TotalTest);
        Assert.Equal(2, category.OutOfScope);
        Assert.Equal(2, category.Passed);
        Assert.Equal(1, category.Failed);
        Assert.Equal(1, category.Blocked);
        Assert.Equal(50f, category.PercentagePassed);
        Assert.Equal("In testing", category.Status);
    }

    [Fact]
    public void Recalculate_ReturnsZeroPercentage_WhenNoInScopeTestsExist()
    {
        var category = new TestCategory
        {
            Tests = new List<Test>
            {
                new() { ScopeStatus = ScopeStatus.OutOfScope, ExecutionStatus = ExecutionStatus.Passed },
                new() { ScopeStatus = ScopeStatus.OutOfScope, ExecutionStatus = ExecutionStatus.Failed }
            }
        };

        category.Recalculate();

        Assert.Equal(0f, category.PercentagePassed);
        Assert.Equal("Not started", category.Status);
    }

    [Fact]
    public void Status_IsCompleted_WhenAllInScopeTestsPassed()
    {
        var category = new TestCategory
        {
            Tests = new List<Test>
            {
                new() { ScopeStatus = ScopeStatus.InScope, ExecutionStatus = ExecutionStatus.Passed },
                new() { ScopeStatus = ScopeStatus.InScope, ExecutionStatus = ExecutionStatus.Passed }
            }
        };

        category.Recalculate();

        Assert.Equal(100f, category.PercentagePassed);
        Assert.Equal("Completed", category.Status);
    }
}
