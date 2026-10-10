using MustRead.App.Services;
using Xunit;

namespace MustRead.Tests.Services;

public class AutostartServiceTests
{
    // Uses a fake exe path — we just test registry read/write behaviour
    private const string FakeExePath = @"C:\Fake\MustRead.exe";

    [Fact]
    public void Enable_Then_IsEnabled_ReturnsTrue()
    {
        try
        {
            AutostartService.Enable(FakeExePath);
            Assert.True(AutostartService.IsEnabled());
        }
        finally
        {
            AutostartService.Disable();
        }
    }

    [Fact]
    public void Disable_Then_IsEnabled_ReturnsFalse()
    {
        AutostartService.Enable(FakeExePath);
        AutostartService.Disable();
        Assert.False(AutostartService.IsEnabled());
    }

    [Fact]
    public void Enable_Twice_DoesNotThrow()
    {
        try
        {
            var ex = Record.Exception(() =>
            {
                AutostartService.Enable(FakeExePath);
                AutostartService.Enable(FakeExePath);
            });
            Assert.Null(ex);
        }
        finally
        {
            AutostartService.Disable();
        }
    }

    [Fact]
    public void Disable_WhenNotEnabled_DoesNotThrow()
    {
        AutostartService.Disable(); // ensure not set
        var ex = Record.Exception(() => AutostartService.Disable());
        Assert.Null(ex);
    }

    [Fact]
    public void IsEnabled_WhenNotSet_ReturnsFalse()
    {
        AutostartService.Disable();
        Assert.False(AutostartService.IsEnabled());
    }
}
