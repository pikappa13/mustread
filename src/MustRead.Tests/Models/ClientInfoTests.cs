using MustRead.App.Models;
using Xunit;

namespace MustRead.Tests.Models;

public class ClientInfoTests
{
    [Fact]
    public void IsOnline_ReturnsTrueWhenLastSeenIsRecent()
    {
        var client = new ClientInfo { LastSeen = DateTime.Now.AddSeconds(-5) };
        Assert.True(client.IsOnline(30));
    }

    [Fact]
    public void IsOnline_ReturnsFalseWhenLastSeenIsOld()
    {
        var client = new ClientInfo { LastSeen = DateTime.Now.AddSeconds(-60) };
        Assert.False(client.IsOnline(30));
    }

    [Fact]
    public void IsOnline_ReturnsTrueJustWithinThreshold()
    {
        // Use a 1-second margin to avoid flakiness from test execution time
        var client = new ClientInfo { LastSeen = DateTime.Now.AddSeconds(-28) };
        Assert.True(client.IsOnline(30));
    }

    [Fact]
    public void DisplayName_FormatsCorrectly()
    {
        var client = new ClientInfo
        {
            Domain = "DOMAIN",
            Username = "user1",
            ComputerName = "PC001",
            SessionType = "RDP"
        };
        Assert.Equal(@"DOMAIN\user1 — PC001 — RDP", client.DisplayName);
    }

    [Fact]
    public void DisplayName_LocalSession()
    {
        var client = new ClientInfo
        {
            Domain = "WORKGROUP",
            Username = "mario",
            ComputerName = "MYPC",
            SessionType = "Local"
        };
        Assert.Equal(@"WORKGROUP\mario — MYPC — Local", client.DisplayName);
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var client = new ClientInfo();
        Assert.Equal("Local", client.SessionType);
        Assert.Equal(string.Empty, client.ClientId);
        Assert.Equal(0, client.SessionId);
    }
}
