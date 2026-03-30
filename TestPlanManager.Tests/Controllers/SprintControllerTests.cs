using Microsoft.AspNetCore.Mvc;
using TestPlanManager.Controllers;
using TestPlanManager.Models;
using TestPlanManager.Tests.TestInfrastructure;

namespace TestPlanManager.Tests.Controllers;

public class SprintControllerTests
{
    [Fact]
    public async Task Get_ReturnsNotFound_WhenSprintDoesNotExist()
    {
        using var ctx = TestContextFactory.Create();
        var controller = new SprintController(ctx);
        ControllerTestHelpers.SetUser(controller);

        var result = await controller.Get(999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenRouteIdDoesNotMatchBodyId()
    {
        using var ctx = TestContextFactory.Create();
        var controller = new SprintController(ctx);
        ControllerTestHelpers.SetUser(controller, AppRoles.Administrator);

        var result = await controller.Update(1, new Sprint { SprintId = 2, BuildNr = "B-2" });

        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenSprintDoesNotExist()
    {
        using var ctx = TestContextFactory.Create();
        var controller = new SprintController(ctx);
        ControllerTestHelpers.SetUser(controller, AppRoles.Administrator);

        var result = await controller.Delete(999);

        Assert.IsType<NotFoundResult>(result);
    }
}
