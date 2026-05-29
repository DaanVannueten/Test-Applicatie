namespace TestPlanManager.Models;

public static class AppRoles
{
    public const string Administrator = "Administrator";
    public const string TestManager = "Test Manager";
    public const string Tester = "Tester";
    public static readonly string[] All = new[] { Administrator, TestManager, Tester };
    public const string Managers = $"{Administrator},{TestManager}";
}
