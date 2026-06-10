namespace MustRead.App.Models;

public class AppSettings
{
    public string ServerPath { get; set; } = string.Empty;
    public bool EnableSender { get; set; } = true;
    public bool EnableReceiver { get; set; } = true;
    public int PollingSeconds { get; set; } = 3;
    public bool AllowSendToAll { get; set; } = true;
    public string ApplicationName { get; set; } = "MustRead";
}
