using Microsoft.AspNetCore.Mvc;
using TestPlanManager.Controllers;
using TestPlanManager.Models;
using TestPlanManager.Tests.TestInfrastructure;

namespace TestPlanManager.Tests.Controllers;

public class TestControllerErrorTests
{
    [Fact]
    public async Task Get_ReturnsNotFound_WhenTestDoesNotExist()
    {
        using var ctx = TestContextFactory.Create();
        var controller = new TestController(ctx);
        ControllerTestHelpers.SetUser(controller);

        var result = await controller.Get(999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task UpdateStatus_ReturnsNotFound_WhenTestDoesNotExist()
    {
        using var ctx = TestContextFactory.Create();
        var controller = new TestController(ctx);
        ControllerTestHelpers.SetUser(controller);

        var dto = new TestController.TestStatusUpdateDto
        {
            ExecutionStatus = ExecutionStatus.Passed,
            Production = "v1",
            Comments = "ok"
        };

        var result = await controller.UpdateStatus(999, dto);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task UpdateStatus_ReturnsNotFound_WhenParentCategoryMissing()
    {
        using var ctx = TestContextFactory.Create();
        ctx.Tests.Add(new Test
        {
            TestId = 1,
            TestCategoryId = 999,
            Name = "T1",
            ScopeStatus = ScopeStatus.InScope,
            ExecutionStatus = ExecutionStatus.NotRun,
            Description = string.Empty,
            Production = string.Empty
        });
        await ctx.SaveChangesAsync();

        var controller = new TestController(ctx);
        ControllerTestHelpers.SetUser(controller);

        var dto = new TestController.TestStatusUpdateDto
        {
            ExecutionStatus = ExecutionStatus.Failed,
            Comments = "broken"
        };

        var result = await controller.UpdateStatus(1, dto);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task UpdateStatus_ClearsExecutedAt_WhenRevertedToNotRun()
    {
        using var ctx = TestContextFactory.Create();

        var category = new TestCategory
        {
            TestCategoryId = 1,
            SprintId = 1,
            Name = "Cat",
            Department = Department.IT
        };

        var test = new Test
        {
            TestId = 1,
            TestCategoryId = 1,
            Name = "T1",
            ScopeStatus = ScopeStatus.InScope,
            ExecutionStatus = ExecutionStatus.Passed,
            ExecutedAt = DateTime.UtcNow,
            Description = string.Empty,
            Production = string.Empty
        };

        category.Tests.Add(test);
        ctx.TestCategories.Add(category);
        await ctx.SaveChangesAsync();

        var controller = new TestController(ctx);
        ControllerTestHelpers.SetUser(controller);

        var dto = new TestController.TestStatusUpdateDto
        {
            ExecutionStatus = ExecutionStatus.NotRun
        };

        var result = await controller.UpdateStatus(1, dto);

        Assert.IsType<NoContentResult>(result);

        var saved = await ctx.Tests.FindAsync(1);
        Assert.NotNull(saved);
        Assert.Null(saved!.ExecutedAt);
    }
}
