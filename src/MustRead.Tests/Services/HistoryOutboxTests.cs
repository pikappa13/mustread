using MustRead.App.Models;
using MustRead.App.Services;
using Xunit;

namespace MustRead.Tests.Services;

/// <summary>
/// The local outbox must keep history records (above all read confirmations)
/// until they reach the share, across offline periods and restarts.
/// </summary>
public class HistoryOutboxTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _shareDir;
    private readonly TestableSettingsService _settings;

    public HistoryOutboxTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"MustReadOutbox_{Guid.NewGuid():N}");
        _shareDir = Path.Combine(_testDir, "share");
        Directory.CreateDirectory(_shareDir);
        _settings = new TestableSettingsService(_testDir);
        _settings.Settings.ServerPath = _shareDir;
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            try { Directory.Delete(_testDir, recursive: true); }
            catch { /* background flush may still hold a handle briefly */ }
        }
    }

    private static ClientInfo Client => new()
    {
        ClientId = "PC01_DOM_mario_1",
        Domain = "DOM",
        Username = "mario",
        ComputerName = "PC01",
        SessionType = "Local"
    };

    private string ReadLog => Path.Combine(_shareDir, "history", "read.jsonl");

    private void GoOffline()
    {
        // A file where the share folder should be: every write to it fails.
        var blocked = Path.Combine(_testDir, "offline-share");
        File.WriteAllText(blocked, "not a directory");
        _settings.Settings.ServerPath = blocked;
    }

    private void GoOnline() => _settings.Settings.ServerPath = _shareDir;

    [Fact]
    public void RecordRead_WhenShareOnline_WritesImmediatelyAndLeavesOutboxEmpty()
    {
        var history = new HistoryService(_settings);
        history.RecordRead("msg-1", Client);

        Assert.Contains("msg-1", File.ReadAllText(ReadLog));
        Assert.Equal(0, history.PendingCount);
    }

    [Fact]
    public void RecordRead_WhenShareOffline_IsKeptAndDeliveredLater()
    {
        var history = new HistoryService(_settings);
        GoOffline();

        history.RecordRead("msg-offline", Client);
        Assert.Equal(1, history.PendingCount);

        GoOnline();
        history.Flush();

        Assert.Equal(0, history.PendingCount);
        Assert.Contains("msg-offline", File.ReadAllText(ReadLog));
    }

    [Fact]
    public void QueuedRecords_SurviveRestart()
    {
        GoOffline();
        new HistoryService(_settings).RecordRead("msg-before-restart", Client);

        GoOnline();
        var afterRestart = new HistoryService(_settings);
        Assert.Equal(1, afterRestart.PendingCount);
        afterRestart.Flush();

        Assert.Contains("msg-before-restart", File.ReadAllText(ReadLog));
    }

    [Fact]
    public void RecordRead_WithoutFlush_OnlyQueuesLocally()
    {
        var history = new HistoryService(_settings);
        history.RecordRead("msg-queued", Client, flush: false);

        Assert.False(File.Exists(ReadLog));
        Assert.Equal(1, history.PendingCount);
    }

    [Fact]
    public void Flush_PreservesOrder()
    {
        var history = new HistoryService(_settings);
        GoOffline();
        for (var i = 0; i < 5; i++)
            history.RecordRead($"ordered-{i}", Client);

        GoOnline();
        history.Flush();

        var lines = File.ReadAllLines(ReadLog);
        Assert.Equal(5, lines.Length);
        for (var i = 0; i < 5; i++)
            Assert.Contains($"ordered-{i}", lines[i]);
    }

    [Fact]
    public async Task ConcurrentFlushFromTwoInstances_DoesNotDuplicate()
    {
        // Two instances of the same user (two RDP sessions) share one outbox.
        var a = new HistoryService(_settings);
        var b = new HistoryService(_settings);
        GoOffline();
        for (var i = 0; i < 20; i++)
            a.RecordRead($"dup-{i}", Client);

        GoOnline();
        await Task.WhenAll(Task.Run(a.Flush), Task.Run(b.Flush), Task.Run(a.Flush), Task.Run(b.Flush));
        a.Flush();

        var lines = File.ReadAllLines(ReadLog);
        Assert.Equal(20, lines.Length);
        Assert.Equal(20, lines.Distinct().Count());
    }

    [Fact]
    public void CorruptOutboxEntry_IsSetAsideAndDoesNotBlockOthers()
    {
        var history = new HistoryService(_settings);
        GoOffline();
        history.RecordRead("good-after-bad", Client);

        var outbox = _settings.GetLocalOutboxPath();
        File.WriteAllText(Path.Combine(outbox, "00000000000000000000-bad.json"), "{ broken");

        GoOnline();
        history.Flush();

        Assert.Contains("good-after-bad", File.ReadAllText(ReadLog));
        Assert.Single(Directory.GetFiles(outbox, "*.corrupt"));
        Assert.Equal(0, history.PendingCount);
    }

    [Fact]
    public void Flush_ReportsOutageToConnectionStatus()
    {
        var status = new ConnectionStatus();
        var history = new HistoryService(_settings, status);
        GoOffline();
        history.RecordRead("status-check", Client);
        Assert.False(status.IsOnline);

        GoOnline();
        history.Flush();
        Assert.True(status.IsOnline);
    }
}
