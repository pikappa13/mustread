using MustRead.App.Models;
using MustRead.App.Utils;
using Xunit;

namespace MustRead.Tests.Utils;

public class JsonHelperTests
{
    [Fact]
    public void Serialize_ProducesValidJson()
    {
        var msg = new AlertMessage
        {
            MessageId = "abc123",
            Body = "Test body",
            TargetType = "User",
            Target = @"DOMAIN\user1"
        };

        var json = JsonHelper.Serialize(msg);

        Assert.Contains("abc123", json);
        Assert.Contains("Test body", json);
    }

    [Fact]
    public void Serialize_UsesCamelCase()
    {
        var msg = new AlertMessage { MessageId = "id1", Body = "corpo" };
        var json = JsonHelper.Serialize(msg);
        Assert.Contains("messageId", json);
        Assert.Contains("body", json);
        Assert.DoesNotContain("MessageId", json);
    }

    [Fact]
    public void Deserialize_RestoresObject()
    {
        var original = new AlertMessage
        {
            MessageId = "round-trip",
            Body = "Testo",
            TargetType = "Computer",
            Target = "PC001",
            SenderUsername = "admin",
            RequiresAcknowledgement = true
        };

        var json = JsonHelper.Serialize(original);
        var restored = JsonHelper.Deserialize<AlertMessage>(json);

        Assert.NotNull(restored);
        Assert.Equal("round-trip", restored.MessageId);
        Assert.Equal("Testo", restored.Body);
        Assert.Equal("Computer", restored.TargetType);
        Assert.True(restored.RequiresAcknowledgement);
    }

    [Fact]
    public void Deserialize_ReturnsNullOnEmptyObject()
    {
        var result = JsonHelper.Deserialize<AlertMessage>("null");
        Assert.Null(result);
    }

    [Fact]
    public void Deserialize_HandlesListOfStrings()
    {
        var list = new List<string> { @"DOMAIN\user1", @"DOMAIN\admin" };
        var json = JsonHelper.Serialize(list);
        var restored = JsonHelper.Deserialize<List<string>>(json);
        Assert.NotNull(restored);
        Assert.Equal(2, restored.Count);
        Assert.Contains(@"DOMAIN\user1", restored);
    }

    [Fact]
    public void Serialize_ClientInfo_RoundTrip()
    {
        var client = new ClientInfo
        {
            ClientId = "PC01_DOM_user1_1",
            Domain = "DOM",
            Username = "user1",
            ComputerName = "PC01",
            SessionId = 1,
            SessionName = "Console",
            SessionType = "Local",
            LastSeen = new DateTime(2026, 6, 10, 12, 0, 0)
        };

        var json = JsonHelper.Serialize(client);
        var restored = JsonHelper.Deserialize<ClientInfo>(json);

        Assert.NotNull(restored);
        Assert.Equal("PC01_DOM_user1_1", restored.ClientId);
        Assert.Equal("Local", restored.SessionType);
        Assert.Equal(new DateTime(2026, 6, 10, 12, 0, 0), restored.LastSeen);
    }
}
