using MustRead.App.Services;
using MustRead.App.Utils;
using Xunit;

namespace MustRead.Tests.Services;

public class MessageSenderServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly TestableSettingsService _settings;
    private readonly SessionInfoService _session;
    private readonly HistoryService _history;
    private readonly MessageSenderService _sender;

    public MessageSenderServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"MustReadSender_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);

        _settings = new TestableSettingsService(_testDir);
        _settings.Settings.ServerPath = _testDir;
        _settings.Settings.EnableSender = true;
        _settings.Settings.AllowSendToAll = true;

        _session = new SessionInfoService();
        _history = new HistoryService(_settings);
        _sender = new MessageSenderService(_settings, _session, _history);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    [Fact]
    public void IsAuthorized_WhenEnabledAndNoAuthFile_ReturnsTrue()
    {
        Assert.True(_sender.IsAuthorized());
    }

    [Fact]
    public void IsAuthorized_WhenDisabled_ReturnsFalse()
    {
        _settings.Settings.EnableSender = false;
        Assert.False(_sender.IsAuthorized());
    }

    [Fact]
    public void IsAuthorized_WhenInAuthFile_ReturnsTrue()
    {
        var configDir = Path.Combine(_testDir, "config");
        Directory.CreateDirectory(configDir);
        var current = _session.GetCurrentSession();
        var identity = $@"{current.Domain}\{current.Username}";
        File.WriteAllText(
            Path.Combine(configDir, "authorized_senders.json"),
            JsonHelper.Serialize(new List<string> { identity }));

        Assert.True(_sender.IsAuthorized());
    }

    [Fact]
    public void IsAuthorized_WhenNotInAuthFile_ReturnsFalse()
    {
        var configDir = Path.Combine(_testDir, "config");
        Directory.CreateDirectory(configDir);
        File.WriteAllText(
            Path.Combine(configDir, "authorized_senders.json"),
            JsonHelper.Serialize(new List<string> { @"OTHER\someone" }));

        Assert.False(_sender.IsAuthorized());
    }

    [Fact]
    public void Send_ToUser_CreatesMessageFileInUserInbox()
    {
        _sender.Send("User", @"DOM\user1", "TEST", "corpo");

        var inboxPath = Path.Combine(_testDir, "inbox", "users", "DOM_user1");
        Assert.True(Directory.Exists(inboxPath));
        var files = Directory.GetFiles(inboxPath, "*.json");
        Assert.Single(files);
    }

    [Fact]
    public void Send_ToComputer_CreatesMessageFileInComputerInbox()
    {
        _sender.Send("Computer", "PC001", "TEST", "corpo");

        var inboxPath = Path.Combine(_testDir, "inbox", "computers", "PC001");
        Assert.True(Directory.Exists(inboxPath));
        Assert.Single(Directory.GetFiles(inboxPath, "*.json"));
    }

    [Fact]
    public void Send_ToAll_CreatesMessageFileInAllInbox()
    {
        _sender.Send("All", "all", "TEST", "broadcast");

        var inboxPath = Path.Combine(_testDir, "inbox", "all");
        Assert.True(Directory.Exists(inboxPath));
        Assert.Single(Directory.GetFiles(inboxPath, "*.json"));
    }

    [Fact]
    public void Send_MessageFileContainsCorrectContent()
    {
        _sender.Send("User", @"DOM\user1", "TITOLO", "Corpo del messaggio");

        var inboxPath = Path.Combine(_testDir, "inbox", "users", "DOM_user1");
        var file = Directory.GetFiles(inboxPath, "*.json").First();
        var content = File.ReadAllText(file, System.Text.Encoding.UTF8);

        Assert.Contains("Corpo del messaggio", content);
        Assert.Contains("TITOLO", content);
        Assert.Contains("User", content);
    }

    [Fact]
    public void Send_WritesToSentHistory()
    {
        _sender.Send("User", @"DOM\user1", "TEST", "corpo");

        var histPath = Path.Combine(_testDir, "history", "sent.jsonl");
        Assert.True(File.Exists(histPath));
        var content = File.ReadAllText(histPath);
        Assert.Contains("corpo", content);
    }

    [Fact]
    public void Send_MultipleTimes_CreatesMultipleFiles()
    {
        _sender.Send("User", @"DOM\user1", "T1", "msg1");
        _sender.Send("User", @"DOM\user1", "T2", "msg2");
        _sender.Send("User", @"DOM\user1", "T3", "msg3");

        var inboxPath = Path.Combine(_testDir, "inbox", "users", "DOM_user1");
        Assert.Equal(3, Directory.GetFiles(inboxPath, "*.json").Length);
    }

    private void WriteAuthFile(string content)
    {
        var configDir = Path.Combine(_testDir, "config");
        Directory.CreateDirectory(configDir);
        File.WriteAllText(Path.Combine(configDir, "authorized_senders.json"), content);
    }

    [Fact]
    public void IsAuthorized_WhenAuthFileMalformed_ReturnsFalse()
    {
        WriteAuthFile("{ not a list ]");
        Assert.False(_sender.IsAuthorized());
    }

    [Fact]
    public void IsAuthorized_WhenRequiredAndAuthFileMissing_ReturnsFalse()
    {
        _settings.Settings.RequireAuthorizedSenders = true;
        Assert.False(_sender.IsAuthorized());
    }

    [Fact]
    public void Send_WhenNotAuthorized_ThrowsAndWritesNothing()
    {
        WriteAuthFile(JsonHelper.Serialize(new List<string> { @"OTHER\someone" }));

        Assert.Throws<UnauthorizedAccessException>(() => _sender.Send("All", "all", "T", "corpo"));
        Assert.False(Directory.Exists(Path.Combine(_testDir, "inbox")));
    }

    [Fact]
    public void Send_WhenAuthListChangesAfterFormOpened_IsRejected()
    {
        Assert.True(_sender.IsAuthorized());
        WriteAuthFile(JsonHelper.Serialize(new List<string> { @"OTHER\someone" }));

        Assert.Throws<UnauthorizedAccessException>(() => _sender.Send("User", @"DOM\user1", "T", "corpo"));
    }

    [Fact]
    public void Send_ToAll_WhenBroadcastDisabled_Throws()
    {
        _settings.Settings.AllowSendToAll = false;
        Assert.Throws<UnauthorizedAccessException>(() => _sender.Send("All", "all", "T", "corpo"));
    }

    [Theory]
    [InlineData("User", @"..\..\evil")]
    [InlineData("User", @"DOM\..")]
    [InlineData("User", @"DOM\a\b")]
    [InlineData("User", "a/b")]
    [InlineData("Computer", "..")]
    [InlineData("Computer", @"PC\..\x")]
    [InlineData("Computer", "")]
    public void Send_WithUnsafeTarget_ThrowsAndWritesNothing(string targetType, string target)
    {
        Assert.Throws<ArgumentException>(() => _sender.Send(targetType, target, "T", "corpo"));
        Assert.Empty(Directory.GetFiles(_testDir, "*.json", SearchOption.AllDirectories));
    }

    [Fact]
    public void Send_WithUnknownTargetType_Throws()
    {
        Assert.Throws<ArgumentException>(() => _sender.Send("Group", "x", "T", "corpo"));
    }

    [Fact]
    public void Send_WithEmptyOrTooLongBody_Throws()
    {
        Assert.Throws<ArgumentException>(() => _sender.Send("All", "all", "T", "   "));
        Assert.Throws<ArgumentException>(() =>
            _sender.Send("All", "all", "T", new string('x', MessageSenderService.MaxBodyLength + 1)));
    }
}
