using MustRead.App.Models;
using MustRead.App.Utils;

namespace MustRead.App.Services;

public class MessageSenderService
{
    private readonly SettingsService _settings;
    private readonly SessionInfoService _session;
    private readonly HistoryService _history;

    public MessageSenderService(SettingsService settings, SessionInfoService session, HistoryService history)
    {
        _settings = settings;
        _session = session;
        _history = history;
    }

    public bool IsAuthorized()
    {
        if (!_settings.Settings.EnableSender)
            return false;

        var configPath = _settings.GetConfigPath("authorized_senders.json");
        if (!File.Exists(configPath))
            return true; // Open mode when file is absent

        try
        {
            var json = File.ReadAllText(configPath);
            var senders = JsonHelper.Deserialize<List<string>>(json) ?? new List<string>();
            var current = _session.GetCurrentSession();
            var identity = $@"{current.Domain}\{current.Username}";
            return senders.Any(s => string.Equals(s, identity, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    public void Send(string targetType, string target, string title, string body)
    {
        var sender = _session.GetCurrentSession();

        var msg = new AlertMessage
        {
            MessageId = AlertMessage.GenerateId(),
            SenderDomain = sender.Domain,
            SenderUsername = sender.Username,
            SenderComputer = sender.ComputerName,
            TargetType = targetType,
            Target = target,
            Title = title,
            Body = body,
            CreatedAt = DateTime.Now,
            RequiresAcknowledgement = true
        };

        var destPath = targetType switch
        {
            "User" => _settings.GetInboxUserPath(
                            target.Contains('\\') ? target.Split('\\')[0] : sender.Domain,
                            target.Contains('\\') ? target.Split('\\')[1] : target),
            "Computer" => _settings.GetInboxComputerPath(target),
            _ => _settings.GetInboxAllPath()
        };

        Directory.CreateDirectory(destPath);
        var msgFile = Path.Combine(destPath, $"{msg.MessageId}.json");
        FileLockHelper.WriteAllTextSafe(msgFile, JsonHelper.Serialize(msg));

        _history.RecordSent(msg, sender);
    }
}
