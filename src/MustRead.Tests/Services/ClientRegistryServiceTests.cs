using MustRead.App.Models;
using MustRead.App.Services;
using MustRead.App.Utils;
using Xunit;

namespace MustRead.Tests.Services;

public class ClientRegistryServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly TestableSettingsService _settings;
    private readonly SessionInfoService _session;
    private readonly ClientRegistryService _registry;

    public ClientRegistryServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"MustReadRegistry_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);

        _settings = new TestableSettingsService(_testDir);
        _settings.Settings.ServerPath = _testDir;
        _settings.Settings.PollingSeconds = 3;

        _session = new SessionInfoService();
        _registry = new ClientRegistryService(_settings, _session);
    }

    public void Dispose()
    {
        _registry.StopHeartbeat();
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    [Fact]
    public void GetOnlineClients_ReturnsEmptyWhenNoFiles()
    {
        var clients = _registry.GetOnlineClients();
        Assert.Empty(clients);
    }

    [Fact]
    public void GetOnlineClients_ReturnsClientWithRecentLastSeen()
    {
        var clientsDir = Path.Combine(_testDir, "clients");
        Directory.CreateDirectory(clientsDir);

        var client = new ClientInfo
        {
            ClientId = "PC01_DOM_user1_1",
            Domain = "DOM",
            Username = "user1",
            ComputerName = "PC01",
            SessionType = "Local",
            LastSeen = DateTime.Now
        };
        File.WriteAllText(
            Path.Combine(clientsDir, "PC01_DOM_user1_1.json"),
            JsonHelper.Serialize(client));

        var clients = _registry.GetOnlineClients();
        Assert.Single(clients);
        Assert.Equal("user1", clients[0].Username);
    }

    [Fact]
    public void GetOnlineClients_IgnoresStaleClients()
    {
        var clientsDir = Path.Combine(_testDir, "clients");
        Directory.CreateDirectory(clientsDir);

        var stale = new ClientInfo
        {
            ClientId = "PC01_DOM_user1_1",
            Domain = "DOM",
            Username = "user1",
            ComputerName = "PC01",
            SessionType = "Local",
            LastSeen = DateTime.Now.AddMinutes(-10)
        };
        File.WriteAllText(
            Path.Combine(clientsDir, "PC01_DOM_user1_1.json"),
            JsonHelper.Serialize(stale));

        var clients = _registry.GetOnlineClients();
        Assert.Empty(clients);
    }

    [Fact]
    public void GetOnlineClients_IgnoresMalformedFiles()
    {
        var clientsDir = Path.Combine(_testDir, "clients");
        Directory.CreateDirectory(clientsDir);
        File.WriteAllText(Path.Combine(clientsDir, "bad.json"), "{ not valid json ]]]");

        var clients = _registry.GetOnlineClients();
        Assert.Empty(clients);
    }

    [Fact]
    public void GetOnlineClients_ReturnsMultipleClients()
    {
        var clientsDir = Path.Combine(_testDir, "clients");
        Directory.CreateDirectory(clientsDir);

        foreach (var name in new[] { "user1", "user2", "user3" })
        {
            var c = new ClientInfo
            {
                ClientId = $"PC01_DOM_{name}_1",
                Domain = "DOM",
                Username = name,
                ComputerName = "PC01",
                SessionType = "Local",
                LastSeen = DateTime.Now
            };
            File.WriteAllText(
                Path.Combine(clientsDir, $"PC01_DOM_{name}_1.json"),
                JsonHelper.Serialize(c));
        }

        var clients = _registry.GetOnlineClients();
        Assert.Equal(3, clients.Count);
    }

    [Fact]
    public void StartHeartbeat_WritesClientFile()
    {
        _registry.StartHeartbeat();
        Thread.Sleep(200); // let timer fire at least once

        var clientsDir = Path.Combine(_testDir, "clients");
        Assert.True(Directory.Exists(clientsDir));
        var files = Directory.GetFiles(clientsDir, "*.json");
        Assert.NotEmpty(files);

        _registry.StopHeartbeat();
    }

    [Fact]
    public void OnlineThreshold_FollowsHeartbeatButNeverBelowFivePolls()
    {
        _settings.Settings.PollingSeconds = 3;
        _settings.Settings.HeartbeatSeconds = 30;
        Assert.Equal(90, _registry.OnlineThresholdSeconds);

        // Clients still writing every polling interval (rc.1) stay online.
        _settings.Settings.HeartbeatSeconds = 2;
        Assert.Equal(15, _registry.OnlineThresholdSeconds);
    }

    [Fact]
    public void RemoveStaleClients_DeletesOnlyClientsGoneLongerThanMaxAge()
    {
        var clientsDir = Path.Combine(_testDir, "clients");
        Directory.CreateDirectory(clientsDir);

        ClientInfo Client(string user, DateTime lastSeen) => new()
        {
            ClientId = $"PC01_DOM_{user}_1",
            Domain = "DOM",
            Username = user,
            ComputerName = "PC01",
            LastSeen = lastSeen
        };
        var gone = Path.Combine(clientsDir, "PC01_DOM_gone_1.json");
        var recent = Path.Combine(clientsDir, "PC01_DOM_recent_1.json");
        FileLockHelper.WriteAllTextSafe(gone, JsonHelper.Serialize(Client("gone", DateTime.Now.AddDays(-8))));
        FileLockHelper.WriteAllTextSafe(recent, JsonHelper.Serialize(Client("recent", DateTime.Now.AddDays(-1))));

        Assert.Equal(1, _registry.RemoveStaleClients(TimeSpan.FromDays(7)));

        Assert.False(File.Exists(gone));
        Assert.False(File.Exists(gone + ".lock"));
        Assert.True(File.Exists(recent));
    }
}
