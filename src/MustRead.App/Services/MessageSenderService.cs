using MustRead.App.Models;
using MustRead.App.Utils;

namespace MustRead.App.Services;

public class MessageSenderService
{
    public const int MaxTitleLength = 200;
    public const int MaxBodyLength = 10_000;

    private static readonly string[] TargetTypes = { "User", "Computer", "All" };

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
        try
        {
            if (!File.Exists(configPath))
                return !_settings.Settings.RequireAuthorizedSenders; // Open mode unless policy requires the list

            var json = File.ReadAllText(configPath);
            var senders = JsonHelper.Deserialize<List<string>>(json) ?? new List<string>();
            var current = _session.GetCurrentSession();
            var identity = $@"{current.Domain}\{current.Username}";
            return senders.Any(s => string.Equals(s, identity, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            // Fail closed: an unreadable or malformed list never grants access.
            Log.Warn($"Lista mittenti autorizzati illeggibile in '{configPath}'", ex);
            return false;
        }
    }

    public void Send(string targetType, string target, string title, string body)
    {
        // Re-checked here, not only when the form opens: the list may change while
        // the form is open, and Send must not depend on the UI for authorization.
        if (!IsAuthorized())
            throw new UnauthorizedAccessException("Non sei autorizzato a inviare messaggi.");

        Validate(targetType, target, title, body);

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
        Log.Info($"Messaggio {msg.MessageId} inviato a {targetType}:{target}");

        _history.RecordSent(msg, sender);
    }

    private void Validate(string targetType, string target, string title, string body)
    {
        if (!TargetTypes.Contains(targetType))
            throw new ArgumentException($"Tipo destinatario non valido: '{targetType}'.");

        if (targetType == "All" && !_settings.Settings.AllowSendToAll)
            throw new UnauthorizedAccessException("L'invio a tutti è disabilitato nelle impostazioni.");

        if (targetType == "User")
        {
            var parts = target.Split('\\');
            if (parts.Length > 2 || !parts.All(PathSafety.IsSafeSegment))
                throw new ArgumentException($"Utente destinatario non valido: '{target}'.");
        }

        if (targetType == "Computer" && !PathSafety.IsSafeSegment(target))
            throw new ArgumentException($"Computer destinatario non valido: '{target}'.");

        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Il messaggio è vuoto.");

        if (title.Length > MaxTitleLength)
            throw new ArgumentException($"Il titolo supera {MaxTitleLength} caratteri.");

        if (body.Length > MaxBodyLength)
            throw new ArgumentException($"Il messaggio supera {MaxBodyLength} caratteri.");
    }
}
