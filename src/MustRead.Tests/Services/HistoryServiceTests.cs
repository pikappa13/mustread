using MustRead.App.Models;
using MustRead.App.Services;
using Xunit;

namespace MustRead.Tests.Services;

public class HistoryServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly TestableSettingsService _settings;
    private readonly HistoryService _history;

    public HistoryServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"MustReadHistory_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);

        _settings = new TestableSettingsService(_testDir);
        _settings.Settings.ServerPath = _testDir;

        _history = new HistoryService(_settings);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    private ClientInfo MakeClient(string username = "user1") => new()
    {
        ClientId = $"PC01_DOM_{username}_1",
        Domain = "DOM",
        Username = username,
        ComputerName = "PC01",
        SessionType = "Local",
        LastSeen = DateTime.Now
    };

    private AlertMessage MakeMessage() => new()
    {
        MessageId = AlertMessage.GenerateId(),
        Body = "Test body",
        TargetType = "User",
        Target = @"DOM\user1",
        SenderUsername = "admin",
        SenderDomain = "DOM",
        SenderComputer = "PC-ADMIN",
        CreatedAt = DateTime.Now
    };

    [Fact]
    public void RecordSent_CreatesSentJsonl()
    {
        _history.RecordSent(MakeMessage(), MakeClient("admin"));
        var path = Path.Combine(_testDir, "history", "sent.jsonl");
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void RecordSent_ContainsSenderInfo()
    {
        var client = MakeClient("admin");
        _history.RecordSent(MakeMessage(), client);
        var content = File.ReadAllText(Path.Combine(_testDir, "history", "sent.jsonl"));
        Assert.Contains("admin", content);
    }

    [Fact]
    public void RecordShown_CreatesShownJsonl()
    {
        var msg = MakeMessage();
        _history.RecordShown(msg.MessageId, MakeClient());
        var path = Path.Combine(_testDir, "history", "shown.jsonl");
        Assert.True(File.Exists(path));
        Assert.Contains(msg.MessageId, File.ReadAllText(path));
    }

    [Fact]
    public void RecordRead_CreatesReadJsonl()
    {
        var msg = MakeMessage();
        _history.RecordRead(msg.MessageId, MakeClient());
        var path = Path.Combine(_testDir, "history", "read.jsonl");
        Assert.True(File.Exists(path));
        Assert.Contains(msg.MessageId, File.ReadAllText(path));
    }

    [Fact]
    public void RecordRead_ContainsSessionType()
    {
        var client = MakeClient();
        client.SessionType = "RDP";
        _history.RecordRead("msg-rdp", client);
        var content = File.ReadAllText(Path.Combine(_testDir, "history", "read.jsonl"));
        Assert.Contains("RDP", content);
    }

    [Fact]
    public void MultipleRecords_AreAllAppended()
    {
        for (int i = 0; i < 5; i++)
            _history.RecordSent(MakeMessage(), MakeClient("admin"));

        var lines = File.ReadAllLines(Path.Combine(_testDir, "history", "sent.jsonl"));
        Assert.Equal(5, lines.Length);
    }

    [Fact]
    public void RecordSent_DoesNotThrowWhenHistoryDirMissing()
    {
        // Directory is created on demand
        var ex = Record.Exception(() => _history.RecordSent(MakeMessage(), MakeClient()));
        Assert.Null(ex);
    }
}
