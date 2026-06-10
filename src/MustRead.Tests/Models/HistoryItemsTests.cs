using MustRead.App.Models;
using Xunit;

namespace MustRead.Tests.Models;

public class HistoryItemsTests
{
    [Fact]
    public void SentHistoryItem_DefaultValues()
    {
        var item = new SentHistoryItem();
        Assert.Equal(string.Empty, item.MessageId);
        Assert.Equal(string.Empty, item.Sender);
        Assert.Equal(string.Empty, item.TargetType);
    }

    [Fact]
    public void ShownHistoryItem_DefaultValues()
    {
        var item = new ShownHistoryItem();
        Assert.Equal(string.Empty, item.MessageId);
        Assert.Equal(string.Empty, item.ShownTo);
        Assert.Equal(string.Empty, item.SessionType);
    }

    [Fact]
    public void ReadHistoryItem_DefaultValues()
    {
        var item = new ReadHistoryItem();
        Assert.Equal(string.Empty, item.MessageId);
        Assert.Equal(string.Empty, item.ReadBy);
        Assert.Equal(string.Empty, item.ReadOnComputer);
    }

    [Fact]
    public void SentHistoryItem_PropertiesRoundtrip()
    {
        var now = DateTime.Now;
        var item = new SentHistoryItem
        {
            MessageId = "msg-1",
            Sender = @"DOMAIN\user1",
            SenderComputer = "PC01",
            TargetType = "User",
            Target = @"DOMAIN\user2",
            Body = "Test",
            SentAt = now
        };
        Assert.Equal("msg-1", item.MessageId);
        Assert.Equal(@"DOMAIN\user1", item.Sender);
        Assert.Equal(now, item.SentAt);
    }
}
