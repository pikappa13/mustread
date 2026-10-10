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
}
