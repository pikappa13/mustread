namespace MustRead.App.Models;

public class AlertMessage
{
    public string MessageId { get; set; } = string.Empty;
    public string SenderDomain { get; set; } = string.Empty;
    public string SenderUsername { get; set; } = string.Empty;
    public string SenderComputer { get; set; } = string.Empty;
    public string TargetType { get; set; } = "User"; // User | Computer | All
    public string Target { get; set; } = string.Empty;
    public string Title { get; set; } = "MESSAGGIO IMPORTANTE";
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool RequiresAcknowledgement { get; set; } = true;

    public static string GenerateId()
    {
        var now = DateTime.Now;
        var suffix = Guid.NewGuid().ToString("N")[..4];
        return $"{now:yyyyMMdd-HHmmss}-{suffix}";
    }
}
