using MustRead.App.Services;
using Xunit;

namespace MustRead.Tests.Services;

public class SessionInfoServiceTests
{
    private readonly SessionInfoService _svc = new();

    [Fact]
    public void GetCurrentSession_ReturnsNonNullResult()
    {
        var info = _svc.GetCurrentSession();
        Assert.NotNull(info);
    }

    [Fact]
    public void GetCurrentSession_UsernameIsNotEmpty()
    {
        var info = _svc.GetCurrentSession();
        Assert.False(string.IsNullOrWhiteSpace(info.Username));
    }

    [Fact]
    public void GetCurrentSession_ComputerNameIsNotEmpty()
    {
        var info = _svc.GetCurrentSession();
        Assert.False(string.IsNullOrWhiteSpace(info.ComputerName));
    }

    [Fact]
    public void GetCurrentSession_ClientIdIsComposite()
    {
        var info = _svc.GetCurrentSession();
        // ClientId = computerName_domain_username_sessionId
        Assert.Contains(info.ComputerName, info.ClientId);
        Assert.Contains(info.Username, info.ClientId);
    }

    [Fact]
    public void GetCurrentSession_SessionTypeIsValidValue()
    {
        var info = _svc.GetCurrentSession();
        Assert.True(info.SessionType == "Local" || info.SessionType == "RDP",
            $"Unexpected SessionType: {info.SessionType}");
    }

    [Fact]
    public void GetCurrentSession_LastSeenIsRecentUtc()
    {
        var before = DateTime.Now.AddSeconds(-2);
        var info = _svc.GetCurrentSession();
        var after = DateTime.Now.AddSeconds(2);
        Assert.InRange(info.LastSeen, before, after);
    }

    [Fact]
    public void GetCurrentSession_SessionIdIsNonNegative()
    {
        var info = _svc.GetCurrentSession();
        Assert.True(info.SessionId >= 0);
    }

    [Fact]
    public void GetCurrentSession_CalledTwice_ReturnsSameUser()
    {
        var a = _svc.GetCurrentSession();
        var b = _svc.GetCurrentSession();
        Assert.Equal(a.Username, b.Username);
        Assert.Equal(a.ComputerName, b.ComputerName);
        Assert.Equal(a.Domain, b.Domain);
    }
}
