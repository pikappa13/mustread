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
    // 1 while a poll is running: a slow share must not stack overlapping polls.
    private int _polling;

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
        if (Interlocked.Exchange(ref _polling, 1) == 1) return;
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
        catch (Exception ex)
        {
            Log.Warn("Polling della cartella condivisa fallito", ex);
        }
        finally
        {
            Interlocked.Exchange(ref _polling, 0);
        }
    }

    private void ProcessMessageFile(string filePath, ClientInfo client)
    {
        AlertMessage? msg;
        try
        {
            var json = File.ReadAllText(filePath);
            msg = JsonHelper.Deserialize<AlertMessage>(json);
        }
        catch (Exception ex)
        {
            Log.Warn($"Messaggio illeggibile: '{filePath}'", ex);
            return;
        }
        if (msg == null || string.IsNullOrWhiteSpace(msg.MessageId)) return;

        lock (_sync)
        {
            // Skip if already acknowledged, or already shown/queued and still
            // pending acknowledgement. Both checks happen atomically so the
            // poll timer can never spawn a duplicate alert.
            if (_readMessageIds.Contains(msg.MessageId)) return;
            if (!_activeMessageIds.Add(msg.MessageId)) return;
        }

        _history.RecordShown(msg.MessageId, client);
        try
        {
            MessageReceived?.Invoke(msg);
        }
        catch (Exception ex)
        {
            // The UI could not take the message: release it so the next poll retries.
            Log.Error($"Visualizzazione del messaggio {msg.MessageId} fallita", ex);
            lock (_sync)
                _activeMessageIds.Remove(msg.MessageId);
        }
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
            SaveReadMessages();
        }

        // The history record goes to the SMB share, which can be slow or locked.
        // Run it off the UI thread so it can never stall the window from closing.
        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                var client = _session.GetCurrentSession();
                _history.RecordRead(messageId, client);
            }
            catch (Exception ex)
            {
                Log.Error($"Registrazione lettura {messageId} sulla share fallita", ex);
            }
        });
    }

    private void LoadReadMessages()
    {
        var path = _settings.GetLocalReadMessagesPath();
        var ids = ReadIdsFile(path);

        if (!File.Exists(path))
        {
            // First run with per-user state: import the v1.0.x machine-wide file
            // so already acknowledged messages are not shown again.
            var legacy = ReadIdsFile(_settings.GetLegacyReadMessagesPath());
            if (legacy.Count > 0)
            {
                ids.UnionWith(legacy);
                Log.Info($"Importati {legacy.Count} messaggi letti dal formato v1.0");
            }
        }

        lock (_sync)
        {
            _readMessageIds.UnionWith(ids);
            if (ids.Count > 0 && !File.Exists(path))
                SaveReadMessages();
        }
    }

    // Must be called while holding _sync.
    private void SaveReadMessages()
    {
        var path = _settings.GetLocalReadMessagesPath();
        try
        {
            // Cross-process lock + merge: another MustRead instance of the same
            // user (e.g. a second RDP session) may have written in the meantime.
            FileLockHelper.WithLock(path, () =>
            {
                var onDisk = ReadIdsFile(path);
                _readMessageIds.UnionWith(onDisk);
                FileLockHelper.WriteAtomic(path, JsonHelper.Serialize(_readMessageIds.ToList()));
            });
        }
        catch (Exception ex)
        {
            Log.Error($"Salvataggio stato letture in '{path}' fallito", ex);
        }
    }

    private static HashSet<string> ReadIdsFile(string path)
    {
        if (!File.Exists(path)) return new HashSet<string>();

        try
        {
            var json = File.ReadAllText(path);
            return new HashSet<string>(JsonHelper.Deserialize<List<string>>(json) ?? new List<string>());
        }
        catch (Exception ex)
        {
            Log.Warn($"Stato letture corrotto in '{path}', salvo una copia e riparto", ex);
            try { File.Copy(path, path + ".corrupt", overwrite: true); } catch { /* best effort */ }
            return new HashSet<string>();
        }
    }
}
