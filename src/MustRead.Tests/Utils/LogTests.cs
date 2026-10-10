using MustRead.App.Utils;
using Xunit;

namespace MustRead.Tests.Utils;

public class LogTests
{
    [Fact]
    public void Warn_WritesLevelMessageAndExceptionToDailyFile()
    {
        var marker = Guid.NewGuid().ToString("N");
        Log.Warn($"test {marker}", new IOException("share offline"));

        var file = Path.Combine(Log.Directory, $"mustread-{DateTime.Now:yyyyMMdd}.log");
        var line = File.ReadAllLines(file).Last(l => l.Contains(marker));
        Assert.Contains("[WARN]", line);
        Assert.Contains("IOException: share offline", line);
    }
}
