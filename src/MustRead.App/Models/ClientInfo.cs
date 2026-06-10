namespace MustRead.App.Models;

public class ClientInfo
{
    public string ClientId { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string ComputerName { get; set; } = string.Empty;
    public int SessionId { get; set; }
    public string SessionName { get; set; } = string.Empty;
    public string SessionType { get; set; } = "Local";
    public DateTime LastSeen { get; set; }

    public bool IsOnline(int thresholdSeconds = 30)
        => (DateTime.UtcNow - LastSeen.ToUniversalTime()).TotalSeconds <= thresholdSeconds;

    public string DisplayName => $@"{Domain}\{Username} — {ComputerName} — {SessionType}";
}
