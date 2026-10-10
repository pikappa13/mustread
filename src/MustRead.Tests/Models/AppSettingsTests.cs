using MustRead.App.Models;
using Xunit;

namespace MustRead.Tests.Models;

public class AppSettingsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var s = new AppSettings();
        Assert.Equal(string.Empty, s.ServerPath);
        Assert.True(s.EnableSender);
        Assert.True(s.EnableReceiver);
        Assert.Equal(3, s.PollingSeconds);
        Assert.True(s.AllowSendToAll);
        Assert.Equal("MustRead", s.ApplicationName);
    }

    [Fact]
    public void Properties_CanBeOverridden()
    {
        var s = new AppSettings
        {
            ServerPath = @"\\SERVER\Share",
            EnableSender = false,
            EnableReceiver = false,
            PollingSeconds = 10,
            AllowSendToAll = false,
            ApplicationName = "Avvisi"
        };

        Assert.Equal(@"\\SERVER\Share", s.ServerPath);
        Assert.False(s.EnableSender);
        Assert.False(s.EnableReceiver);
        Assert.Equal(10, s.PollingSeconds);
        Assert.False(s.AllowSendToAll);
        Assert.Equal("Avvisi", s.ApplicationName);
    }
}
