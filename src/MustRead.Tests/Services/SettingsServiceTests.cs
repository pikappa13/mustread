using MustRead.App.Services;
using MustRead.App.Utils;
using Xunit;

namespace MustRead.Tests.Services;

public class SettingsServiceTests : IDisposable
{
    private readonly string _testDir;

    public SettingsServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"MustReadSettings_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    private SettingsService MakeService(string? json = null)
    {
        var settingsPath = Path.Combine(_testDir, "settings.json");
        if (json != null)
            File.WriteAllText(settingsPath, json);

        // Use reflection to override the private constant path for testing
        var svc = new TestableSettingsService(_testDir);
        svc.Load();
        return svc;
    }

    [Fact]
    public void Load_WhenFileNotExists_UsesDefaults()
    {
        var svc = MakeService();
        Assert.Equal(string.Empty, svc.Settings.ServerPath);
        Assert.Equal(3, svc.Settings.PollingSeconds);
        Assert.True(svc.Settings.EnableReceiver);
    }

    [Fact]
    public void Load_ParsesServerPath()
    {
        var svc = MakeService(@"{""serverPath"":""\\\\SERVER\\MustRead"",""pollingSeconds"":5}");
        Assert.Equal(@"\\SERVER\MustRead", svc.Settings.ServerPath);
        Assert.Equal(5, svc.Settings.PollingSeconds);
    }

    [Fact]
    public void Load_HandlesMalformedJson_UsesDefaults()
    {
        var svc = MakeService("{ this is not valid json }");
        Assert.Equal(string.Empty, svc.Settings.ServerPath);
    }

    [Fact]
    public void Save_WritesFileOnDisk()
    {
        var svc = MakeService();
        svc.Settings.ServerPath = @"\\TEST\Share";
        svc.Settings.PollingSeconds = 7;
        svc.Save();

        var path = Path.Combine(_testDir, "settings.json");
        Assert.True(File.Exists(path));
        var content = File.ReadAllText(path);
        Assert.Contains("serverPath", content);
    }

    [Fact]
    public void Save_ThenLoad_RoundTrip()
    {
        var svc = MakeService();
        svc.Settings.ServerPath = @"\\ROUND\TRIP";
        svc.Settings.PollingSeconds = 9;
        svc.Settings.EnableSender = false;
        svc.Save();

        var svc2 = new TestableSettingsService(_testDir);
        svc2.Load();
        Assert.Equal(@"\\ROUND\TRIP", svc2.Settings.ServerPath);
        Assert.Equal(9, svc2.Settings.PollingSeconds);
        Assert.False(svc2.Settings.EnableSender);
    }

    [Fact]
    public void PathHelpers_BuildCorrectPaths()
    {
        var svc = MakeService(@"{""serverPath"":""\\\\SRV\\MR""}");

        Assert.Equal(@"\\SRV\MR\inbox\users\DOM_user1", svc.GetInboxUserPath("DOM", "user1"));
        Assert.Equal(@"\\SRV\MR\inbox\computers\PC01", svc.GetInboxComputerPath("PC01"));
        Assert.Equal(@"\\SRV\MR\inbox\all", svc.GetInboxAllPath());
        Assert.Equal(@"\\SRV\MR\clients", svc.GetClientsPath());
        Assert.Equal(@"\\SRV\MR\history\sent.jsonl", svc.GetHistoryPath("sent.jsonl"));
        Assert.Equal(@"\\SRV\MR\config\authorized_senders.json", svc.GetConfigPath("authorized_senders.json"));
    }
}

/// <summary>
/// Subclass that overrides file paths to use a temp directory during tests.
/// </summary>
internal class TestableSettingsService : SettingsService
{
    private readonly string _baseDir;

    public TestableSettingsService(string baseDir) => _baseDir = baseDir;

    protected override string LocalSettingsPath => Path.Combine(_baseDir, "settings.json");
    protected override string LocalReadMessagesPath => Path.Combine(_baseDir, "read_messages.json");
    protected override string LegacyReadMessagesPath => Path.Combine(_baseDir, "legacy_read_messages.json");
}
