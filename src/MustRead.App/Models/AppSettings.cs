namespace MustRead.App.Models;

public class AppSettings
{
    public string ServerPath { get; set; } = string.Empty;
    public bool EnableSender { get; set; } = true;
    public bool EnableReceiver { get; set; } = true;
    public int PollingSeconds { get; set; } = 3;
    public bool AllowSendToAll { get; set; } = true;
    public string ApplicationName { get; set; } = "MustRead";

    /// <summary>
    /// When true, sending is denied unless config/authorized_senders.json exists on
    /// the share and lists the current user. When false (default, v1.0 behaviour)
    /// a missing file means anyone with EnableSender can send.
    /// </summary>
    public bool RequireAuthorizedSenders { get; set; } = false;
}
