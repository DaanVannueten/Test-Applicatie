using Microsoft.AspNetCore.Mvc;
using TestPlanManager.Controllers;
using TestPlanManager.Data;
using TestPlanManager.Models;
using TestPlanManager.Tests.TestInfrastructure;

namespace TestPlanManager.Tests.Controllers;

public class HomeControllerTests
{
    [Fact]
    public async Task Index_RedirectsToVersionPage_WhenNoSprintIdAndNoTemplateView()
    {
        using var ctx = TestContextFactory.Create();
        var store = new StubDefaultVersionStore();
        var controller = new HomeController(ctx, store);
        ControllerTestHelpers.SetUser(controller);

        var result = await controller.Index(null, includeTemplates: false);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("TestPlanVersion", redirect.ControllerName);
    }

    [Fact]
    public async Task ArchiveCycle_RedirectsWithError_WhenSprintMissing()
    {
        using var ctx = TestContextFactory.Create();
        var store = new StubDefaultVersionStore();
        var controller = new HomeController(ctx, store);
        ControllerTestHelpers.SetUser(controller, AppRoles.Tester);

        var result = await controller.ArchiveCycle(999);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Test cycle not found.", controller.TempData["ErrorMessage"]);
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
