using MustRead.App.Models;
using MustRead.App.Services;
using MustRead.App.Utils;
using Xunit;

namespace MustRead.Tests.Services;

public class MessageReceiverServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly TestableSettingsService _settings;
    private readonly SessionInfoService _session;
    private readonly HistoryService _history;
    private readonly MessageReceiverService _receiver;

    public MessageReceiverServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"MustReadReceiver_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);

        _settings = new TestableSettingsService(_testDir);
        _settings.Settings.ServerPath = _testDir;
        _settings.Settings.PollingSeconds = 1;

        _session = new SessionInfoService();
        _history = new HistoryService(_settings);
        _receiver = new MessageReceiverService(_settings, _session, _history);
    }

    public void Dispose()
    {
        _receiver.Stop();
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    private AlertMessage WriteMessageToInbox(string folder, string body = "test body")
    {
        Directory.CreateDirectory(folder);
        var msg = new AlertMessage
        {
            MessageId = AlertMessage.GenerateId(),
            Body = body,
            Title = "TEST",
            TargetType = "All",
            Target = "all",
            CreatedAt = DateTime.Now,
            RequiresAcknowledgement = true
        };
        File.WriteAllText(Path.Combine(folder, $"{msg.MessageId}.json"), JsonHelper.Serialize(msg));
        return msg;
    }

    [Fact]
    public void Start_And_Stop_DoNotThrow()
    {
        var ex = Record.Exception(() =>
        {
            _receiver.Start();
            Thread.Sleep(200);
            _receiver.Stop();
        });
        Assert.Null(ex);
    }

    [Fact]
    public void MessageReceived_FiredForNewMessageInAllInbox()
    {
        var received = new List<AlertMessage>();
        _receiver.MessageReceived += msg => received.Add(msg);

        var allInbox = Path.Combine(_testDir, "inbox", "all");
        WriteMessageToInbox(allInbox, "broadcast message");

        _receiver.Start();
        Thread.Sleep(400);
        _receiver.Stop();

        Assert.Single(received);
        Assert.Equal("broadcast message", received[0].Body);
    }

    [Fact]
    public void MessageReceived_FiredForUserInbox()
    {
        var session = _session.GetCurrentSession();
        var userInbox = Path.Combine(_testDir, "inbox", "users", $"{session.Domain}_{session.Username}");

        var received = new List<AlertMessage>();
        _receiver.MessageReceived += msg => received.Add(msg);

        WriteMessageToInbox(userInbox, "user specific");
        _receiver.Start();
        Thread.Sleep(400);
        _receiver.Stop();

        Assert.Single(received);
        Assert.Equal("user specific", received[0].Body);
    }

    [Fact]
    public void MessageReceived_NotFiredTwiceForSameMessage()
    {
        var received = new List<AlertMessage>();
        _receiver.MessageReceived += msg => received.Add(msg);

        var allInbox = Path.Combine(_testDir, "inbox", "all");
        var msg = WriteMessageToInbox(allInbox);

        _receiver.Start();
        Thread.Sleep(200);

        // Mark as read and wait for another poll
        _receiver.MarkAsRead(msg.MessageId);
        Thread.Sleep(300);
        _receiver.Stop();

        Assert.Single(received); // only once
    }

    [Fact]
    public void MarkAsRead_WritesToShownAndReadHistory()
    {
        var allInbox = Path.Combine(_testDir, "inbox", "all");
        var msg = WriteMessageToInbox(allInbox);

        _receiver.Start();
        Thread.Sleep(300);
        _receiver.MarkAsRead(msg.MessageId);
        Thread.Sleep(100);
        _receiver.Stop();

        var shownPath = Path.Combine(_testDir, "history", "shown.jsonl");
        var readPath = Path.Combine(_testDir, "history", "read.jsonl");

        Assert.True(File.Exists(shownPath));
        Assert.True(File.Exists(readPath));
        Assert.Contains(msg.MessageId, File.ReadAllText(readPath));
    }

    [Fact]
    public void MarkAsRead_PersistsAcrossNewInstance()
    {
        var allInbox = Path.Combine(_testDir, "inbox", "all");
        var msg = WriteMessageToInbox(allInbox);

        // First instance: receive and mark as read
        _receiver.Start();
        Thread.Sleep(300);
        _receiver.MarkAsRead(msg.MessageId);
        _receiver.Stop();

        // Second instance: should NOT fire MessageReceived again
        var receiver2 = new MessageReceiverService(_settings, _session, _history);
        var received = new List<AlertMessage>();
        receiver2.MessageReceived += m => received.Add(m);
        receiver2.Start();
        Thread.Sleep(400);
        receiver2.Stop();

        Assert.Empty(received);
    }

    [Fact]
    public void MalformedMessageFile_IsIgnored()
    {
        var allInbox = Path.Combine(_testDir, "inbox", "all");
        Directory.CreateDirectory(allInbox);
        File.WriteAllText(Path.Combine(allInbox, "bad.json"), "{ not json ]]]");

        var received = new List<AlertMessage>();
        _receiver.MessageReceived += msg => received.Add(msg);
        _receiver.Start();
        Thread.Sleep(300);
        _receiver.Stop();

        Assert.Empty(received);
    }
}
