namespace MustRead.App.Models;

public class AppSettings
{
    public string ServerPath { get; set; } = string.Empty;
    public bool EnableSender { get; set; } = true;
    public bool EnableReceiver { get; set; } = true;
    public int PollingSeconds { get; set; } = 3;

    /// <summary>
    /// How often each client rewrites its presence file in clients\. Kept apart
    /// from <see cref="PollingSeconds"/>: presence does not need to be as fresh as
    /// message delivery, and every write costs several operations on the share.
    /// </summary>
    public int HeartbeatSeconds { get; set; } = 10;

    /// <summary>
    /// Days a sent message stays valid. 0 (default) means it never expires.
    /// Stamped on the message by the sender; expired messages are not shown and
    /// are removed from the inbox by authorized senders.
    /// </summary>
    public int MessageRetentionDays { get; set; } = 0;
    public bool AllowSendToAll { get; set; } = true;
    public string ApplicationName { get; set; } = "MustRead";

    /// <summary>
    /// When true, sending is denied unless config/authorized_senders.json exists on
    /// the share and lists the current user. When false (default, v1.0 behaviour)
    /// a missing file means anyone with EnableSender can send.
    /// </summary>
    public bool RequireAuthorizedSenders { get; set; } = false;
}
