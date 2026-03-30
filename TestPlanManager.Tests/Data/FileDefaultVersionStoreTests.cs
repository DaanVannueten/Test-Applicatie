using TestPlanManager.Data;

namespace TestPlanManager.Tests.Data;

public class FileDefaultVersionStoreTests : IDisposable
{
    private readonly string _tempRoot;

    public FileDefaultVersionStoreTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "tpm-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public void GetDefaultSprintId_ReturnsNull_WhenFileDoesNotExist()
    {
        var store = new FileDefaultVersionStore(_tempRoot);

        var result = store.GetDefaultSprintId();

        Assert.Null(result);
    }

    [Fact]
    public void SetDefaultSprintId_ThenGetDefaultSprintId_ReturnsSavedValue()
    {
        var store = new FileDefaultVersionStore(_tempRoot);

        store.SetDefaultSprintId(42);
        var result = store.GetDefaultSprintId();

        Assert.Equal(42, result);
    }

    [Fact]
    public void GetDefaultSprintId_ReturnsNull_WhenJsonIsInvalid()
    {
        var store = new FileDefaultVersionStore(_tempRoot);
        var filePath = Path.Combine(_tempRoot, "App_Data", "default-version.json");
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, "{ invalid json }");

        var result = store.GetDefaultSprintId();

        Assert.Null(result);
    }

    [Fact]
    public void SetDefaultSprintId_WithNull_CanBeReadBackAsNull()
    {
        var store = new FileDefaultVersionStore(_tempRoot);

        store.SetDefaultSprintId(null);
        var result = store.GetDefaultSprintId();

        Assert.Null(result);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
