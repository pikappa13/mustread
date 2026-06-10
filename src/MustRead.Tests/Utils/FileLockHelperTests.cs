using MustRead.App.Utils;
using Xunit;

namespace MustRead.Tests.Utils;

public class FileLockHelperTests : IDisposable
{
    private readonly string _testDir;

    public FileLockHelperTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"MustReadTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    [Fact]
    public void AppendLine_CreatesFileIfNotExists()
    {
        var path = Path.Combine(_testDir, "test.jsonl");
        FileLockHelper.AppendLine(path, "{\"test\":1}");
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void AppendLine_WritesContent()
    {
        var path = Path.Combine(_testDir, "test.jsonl");
        FileLockHelper.AppendLine(path, "line1");
        var content = File.ReadAllText(path);
        Assert.Contains("line1", content);
    }

    [Fact]
    public void AppendLine_AppendsMultipleLines()
    {
        var path = Path.Combine(_testDir, "multi.jsonl");
        FileLockHelper.AppendLine(path, "line1");
        FileLockHelper.AppendLine(path, "line2");
        FileLockHelper.AppendLine(path, "line3");

        var lines = File.ReadAllLines(path);
        Assert.Equal(3, lines.Length);
        Assert.Equal("line1", lines[0]);
        Assert.Equal("line2", lines[1]);
        Assert.Equal("line3", lines[2]);
    }

    [Fact]
    public void WriteAllTextSafe_CreatesFile()
    {
        var path = Path.Combine(_testDir, "settings.json");
        FileLockHelper.WriteAllTextSafe(path, "{\"key\":\"value\"}");
        Assert.True(File.Exists(path));
        Assert.Contains("value", File.ReadAllText(path));
    }

    [Fact]
    public void WriteAllTextSafe_OverwritesExistingContent()
    {
        var path = Path.Combine(_testDir, "overwrite.json");
        FileLockHelper.WriteAllTextSafe(path, "original");
        FileLockHelper.WriteAllTextSafe(path, "replaced");
        Assert.Equal("replaced", File.ReadAllText(path));
    }

    [Fact]
    public void AppendLine_CreatesParentDirectoryIfMissing()
    {
        var path = Path.Combine(_testDir, "subdir", "deep", "test.jsonl");
        FileLockHelper.AppendLine(path, "data");
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task AppendLine_ConcurrentWrites_DoNotCorruptFile()
    {
        var path = Path.Combine(_testDir, "concurrent.jsonl");
        var tasks = Enumerable.Range(0, 20).Select(i =>
            Task.Run(() => FileLockHelper.AppendLine(path, $"{{\"i\":{i}}}"))).ToArray();

        await Task.WhenAll(tasks);

        var lines = File.ReadAllLines(path);
        Assert.Equal(20, lines.Length);
        Assert.All(lines, l => Assert.False(string.IsNullOrWhiteSpace(l)));
    }
}
