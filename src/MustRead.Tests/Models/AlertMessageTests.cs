using MustRead.App.Models;
using Xunit;

namespace MustRead.Tests.Models;

public class AlertMessageTests
{
    [Fact]
    public void GenerateId_ReturnsNonEmptyString()
    {
        var id = AlertMessage.GenerateId();
        Assert.False(string.IsNullOrWhiteSpace(id));
    }

    [Fact]
    public void GenerateId_MatchesExpectedFormat()
    {
        var id = AlertMessage.GenerateId();
        // Format: yyyyMMdd-HHmmss-xxxxxxxxxxxx
        Assert.Matches(@"^\d{8}-\d{6}-[a-f0-9]{12}$", id);
    }

    [Fact]
    public void GenerateId_IsDifferentEachCall()
    {
        var id1 = AlertMessage.GenerateId();
        var id2 = AlertMessage.GenerateId();
        // May collide within the same second but suffix is random — overwhelmingly different
        Assert.True(id1 != id2 || id1.Split('-')[2] != id2.Split('-')[2]);
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var msg = new AlertMessage();
        Assert.Equal("MESSAGGIO IMPORTANTE", msg.Title);
        Assert.True(msg.RequiresAcknowledgement);
        Assert.Equal("User", msg.TargetType);
        Assert.Equal(string.Empty, msg.MessageId);
        Assert.Equal(string.Empty, msg.Body);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var msg = new AlertMessage
        {
            MessageId = "test-id",
            SenderDomain = "DOMAIN",
            SenderUsername = "user1",
            SenderComputer = "PC01",
            TargetType = "All",
            Target = "all",
            Title = "TEST",
            Body = "Corpo del messaggio",
            CreatedAt = new DateTime(2026, 1, 1, 12, 0, 0),
            RequiresAcknowledgement = false
        };

        Assert.Equal("test-id", msg.MessageId);
        Assert.Equal("DOMAIN", msg.SenderDomain);
        Assert.Equal("user1", msg.SenderUsername);
        Assert.Equal("All", msg.TargetType);
        Assert.Equal("Corpo del messaggio", msg.Body);
        Assert.False(msg.RequiresAcknowledgement);
    }

    [Theory]
    [InlineData(@"DOM\mario", "", "DOM", "mario", true)]
    [InlineData(@"dom\MARIO", "", "DOM", "mario", true)]
    [InlineData("mario", "DOM", "DOM", "mario", true)]
    [InlineData(@"DOM_A\b", "", "DOM", "A_b", false)]   // same inbox folder "DOM_A_b"
    [InlineData(@"DOM\A_b", "", "DOM_A", "b", false)]
    [InlineData("mario", "ALTRO", "DOM", "mario", false)]
    public void IsAddressedTo_UserTarget(string target, string senderDomain, string domain, string user, bool expected)
    {
        var msg = new AlertMessage { TargetType = "User", Target = target, SenderDomain = senderDomain };
        Assert.Equal(expected, msg.IsAddressedTo(domain, user));
    }

    [Theory]
    [InlineData("Computer", "PC01")]
    [InlineData("All", "all")]
    public void IsAddressedTo_NonUserTargets_AlwaysMatch(string targetType, string target)
    {
        var msg = new AlertMessage { TargetType = targetType, Target = target };
        Assert.True(msg.IsAddressedTo("DOM", "mario"));
    }

    [Fact]
    public void IsExpired_UsesExpiresAt()
    {
        var now = DateTime.Now;
        Assert.False(new AlertMessage().IsExpired(now));
        Assert.False(new AlertMessage { ExpiresAt = now.AddMinutes(1) }.IsExpired(now));
        Assert.True(new AlertMessage { ExpiresAt = now.AddMinutes(-1) }.IsExpired(now));
    }

    [Fact]
    public void ExpiresAt_NotWrittenWhenNull_KeepsV11FileFormat()
    {
        var json = MustRead.App.Utils.JsonHelper.Serialize(new AlertMessage { MessageId = "x" });
        Assert.DoesNotContain("expiresAt", json);
    }
}
