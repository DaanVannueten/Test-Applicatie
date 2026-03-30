using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace TestPlanManager.Tests.TestInfrastructure;

internal static class ControllerTestHelpers
{
    public static void SetUser(ControllerBase controller, params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "test-user") };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            }
        };
    }

    public static void SetUser(Controller controller, params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "test-user") };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            }
        };

        controller.TempData = new TempDataDictionary(controller.HttpContext, new NoopTempDataProvider());
    }

    private sealed class NoopTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
