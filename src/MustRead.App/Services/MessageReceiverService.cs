using MustRead.App.Models;
using MustRead.App.Utils;

namespace MustRead.App.Services;

public class MessageReceiverService
{
    private readonly SettingsService _settings;
    private readonly SessionInfoService _session;
    private readonly HistoryService _history;

    private HashSet<string> _readMessageIds = new();
    private System.Threading.Timer? _pollTimer;

    public event Action<AlertMessage>? MessageReceived;

    public MessageReceiverService(SettingsService settings, SessionInfoService session, HistoryService history)
    {
        _settings = settings;
        _session = session;
        _history = history;
    }

    public void Start()
    {
        LoadReadMessages();
        Poll(null);
        _pollTimer = new System.Threading.Timer(
            Poll,
            null,
            TimeSpan.FromSeconds(_settings.Settings.PollingSeconds),
            TimeSpan.FromSeconds(_settings.Settings.PollingSeconds));
    }

    public void Stop()
    {
        _pollTimer?.Dispose();
    }

    private void Poll(object? _)
    {
        try
        {
            var client = _session.GetCurrentSession();
            var folders = new[]
            {
                _settings.GetInboxUserPath(client.Domain, client.Username),
                _settings.GetInboxComputerPath(client.ComputerName),
                _settings.GetInboxAllPath()
            };

            foreach (var folder in folders)
            {
                if (!Directory.Exists(folder)) continue;

                foreach (var file in Directory.GetFiles(folder, "*.json"))
                {
                    ProcessMessageFile(file, client);
                }
            }
        }
        catch { }
    }

    private void ProcessMessageFile(string filePath, ClientInfo client)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            var msg = JsonHelper.Deserialize<AlertMessage>(json);
            if (msg == null) return;
            if (_readMessageIds.Contains(msg.MessageId)) return;

            _history.RecordShown(msg.MessageId, client);
            MessageReceived?.Invoke(msg);
        }
        catch { }
    }

    public void MarkAsRead(string messageId)
    {
        _readMessageIds.Add(messageId);
        SaveReadMessages();

        try
        {
            var client = _session.GetCurrentSession();
            _history.RecordRead(messageId, client);
        }
        catch { }
    }

    private void LoadReadMessages()
    {
        var path = _settings.GetLocalReadMessagesPath();
        if (!File.Exists(path)) return;

        try
        {
            var json = File.ReadAllText(path);
            var ids = JsonHelper.Deserialize<List<string>>(json);
            if (ids != null)
                _readMessageIds = new HashSet<string>(ids);
        }
        catch { }
    }

    private void SaveReadMessages()
    {
        try
        {
            var path = _settings.GetLocalReadMessagesPath();
            var dir = Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(dir);
            FileLockHelper.WriteAllTextSafe(path, JsonHelper.Serialize(_readMessageIds.ToList()));
        }
        catch { }
    }
}
