using Microsoft.AspNetCore.Mvc;
using TestPlanManager.Controllers;
using TestPlanManager.Data;
using TestPlanManager.Models;
using TestPlanManager.Tests.TestInfrastructure;

namespace TestPlanManager.Tests.Controllers;

public class TestPlanVersionControllerErrorTests
{
    [Fact]
    public async Task Create_ReturnsForbid_WhenUserHasNoManagerRole()
    {
        using var ctx = TestContextFactory.Create();
        var store = new StubDefaultVersionStore();
        var controller = new TestPlanVersionController(ctx, store);
        ControllerTestHelpers.SetUser(controller, AppRoles.Tester);

        var result = await controller.Create(new CreateVersionInputModel { BuildNr = "B-1" });

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Create_RedirectsToIndex_WhenModelStateInvalid()
    {
        using var ctx = TestContextFactory.Create();
        var store = new StubDefaultVersionStore();
        var controller = new TestPlanVersionController(ctx, store);
        ControllerTestHelpers.SetUser(controller, AppRoles.Administrator);
        controller.ModelState.AddModelError("BuildNr", "required");

        var result = await controller.Create(new CreateVersionInputModel { BuildNr = string.Empty });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("New build number is invalid.", controller.TempData["ErrorMessage"]);
    }

    [Fact]
    public async Task Copy_RedirectsToIndex_WhenSourceIsMissing()
    {
        using var ctx = TestContextFactory.Create();
        ctx.Sprints.Add(new Sprint { SprintId = 1, BuildNr = "existing", IsTemplate = true });
        await ctx.SaveChangesAsync();

        var store = new StubDefaultVersionStore();
        var controller = new TestPlanVersionController(ctx, store);
        ControllerTestHelpers.SetUser(controller, AppRoles.TestManager);

        var input = new CopyVersionInputModel
        {
            BuildNr = "new-cycle",
            CopyFromSprintId = 999
        };

        var result = await controller.Copy(input);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("The source version to copy does not exist.", controller.TempData["ErrorMessage"]);
    }

    [Fact]
    public async Task SetDefault_RedirectsWithError_WhenSprintIsTemplate()
    {
        using var ctx = TestContextFactory.Create();
        ctx.Sprints.Add(new Sprint { SprintId = 1, BuildNr = "tmpl", IsTemplate = true });
        await ctx.SaveChangesAsync();

        var store = new StubDefaultVersionStore();
        var controller = new TestPlanVersionController(ctx, store);
        ControllerTestHelpers.SetUser(controller, AppRoles.Administrator);

        var result = await controller.SetDefault(1);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Template versions cannot be set as dashboard default.", controller.TempData["ErrorMessage"]);
    }

    private sealed class StubDefaultVersionStore : IDefaultVersionStore
    {
        private int? _value;

        public int? GetDefaultSprintId() => _value;

        public void SetDefaultSprintId(int? sprintId)
        {
            _value = sprintId;
        }
    }
}
