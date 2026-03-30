using TestPlanManager.Models;

namespace TestPlanManager.Tests.Models;

public class TestTitleSanitizerTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData(" Login test ", "Login test")]
    [InlineData("Login test,", "Login test")]
    [InlineData("Login test'", "Login test")]
    [InlineData("Login test\u2019   ", "Login test")]
    public void Clean_ReturnsExpectedValue(string? input, string expected)
    {
        var actual = TestTitleSanitizer.Clean(input);

        Assert.Equal(expected, actual);
    }
}
