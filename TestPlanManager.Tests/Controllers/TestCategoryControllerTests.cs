using Microsoft.AspNetCore.Mvc;
using TestPlanManager.Controllers;
using TestPlanManager.Tests.TestInfrastructure;

namespace TestPlanManager.Tests.Controllers;

public class TestCategoryControllerTests
{
    [Fact]
    public async Task GetDetail_ReturnsNotFound_WhenCategoryDoesNotExist()
    {
        using var ctx = TestContextFactory.Create();
        var controller = new TestCategoryController(ctx);
        ControllerTestHelpers.SetUser(controller);

        var result = await controller.GetDetail(999);

        Assert.IsType<NotFoundResult>(result);
    }
}
