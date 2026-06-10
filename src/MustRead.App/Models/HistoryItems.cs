namespace MustRead.App.Models;

public class SentHistoryItem
{
    public string MessageId { get; set; } = string.Empty;
    public string Sender { get; set; } = string.Empty;
    public string SenderComputer { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
}

public class ShownHistoryItem
{
    public string MessageId { get; set; } = string.Empty;
    public string ShownTo { get; set; } = string.Empty;
    public string ShownOnComputer { get; set; } = string.Empty;
    public string SessionType { get; set; } = string.Empty;
    public DateTime ShownAt { get; set; }
}

public class ReadHistoryItem
{
    public string MessageId { get; set; } = string.Empty;
    public string ReadBy { get; set; } = string.Empty;
    public string ReadOnComputer { get; set; } = string.Empty;
    public string SessionType { get; set; } = string.Empty;
    public DateTime ReadAt { get; set; }
}
