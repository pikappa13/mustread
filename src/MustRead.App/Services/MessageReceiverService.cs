using MustRead.App.Models;
using MustRead.App.Utils;

namespace MustRead.App.Services;

public class MessageReceiverService
{
    private readonly SettingsService _settings;
    private readonly SessionInfoService _session;
    private readonly HistoryService _history;

    private HashSet<string> _readMessageIds = new();
    // Messages already handed off to the UI (shown or queued). Guards against the
    // poll timer re-opening an alert for a message whose form is still on screen
    // or still being acknowledged. Separate from _readMessageIds so it covers the
    // window between "shown" and "marked read".
    private readonly HashSet<string> _activeMessageIds = new();
    private readonly object _sync = new();
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

            lock (_sync)
            {
                // Skip if already acknowledged, or already shown/queued and still
                // pending acknowledgement. Both checks happen atomically so the
                // poll timer can never spawn a duplicate alert.
                if (_readMessageIds.Contains(msg.MessageId)) return;
                if (!_activeMessageIds.Add(msg.MessageId)) return;
            }

            _history.RecordShown(msg.MessageId, client);
            MessageReceived?.Invoke(msg);
        }
        catch { }
    }

    public void MarkAsRead(string messageId)
    {
        // Update the in-memory guards and persist the read-state, all under the
        // same lock. The lock does three things atomically:
        //  - flips the guard the poll timer reads (so it can't re-open the alert),
        //  - serialises the snapshot+write so concurrent acks can't lose-update the
        //    file (an older snapshot overwriting a newer one),
        //  - keeps the on-disk file consistent with the in-memory set.
        // The local write is a fast local-disk op, so holding the lock is fine.
        lock (_sync)
        {
            _readMessageIds.Add(messageId);
            _activeMessageIds.Remove(messageId);
            SaveReadMessages(_readMessageIds.ToList());
        }

        // The history record goes to the SMB share, which can be slow or locked
        // (FileLockHelper retries up to ~2s). Run it off the UI thread so it can
        // never stall the window from closing.
        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                var client = _session.GetCurrentSession();
                _history.RecordRead(messageId, client);
            }
            catch { }
        });
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
                lock (_sync)
                    _readMessageIds = new HashSet<string>(ids);
        }
        catch { }
    }

    private void SaveReadMessages(List<string> ids)
    {
        try
        {
            var path = _settings.GetLocalReadMessagesPath();
            var dir = Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(dir);
            FileLockHelper.WriteAllTextSafe(path, JsonHelper.Serialize(ids));
        }
        catch { }
    }
}
