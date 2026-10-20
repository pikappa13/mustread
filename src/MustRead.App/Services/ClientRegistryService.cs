using MustRead.App.Models;
using MustRead.App.Utils;

namespace MustRead.App.Services;

public class ClientRegistryService
{
    private readonly SettingsService _settings;
    private readonly SessionInfoService _session;
    private readonly ConnectionStatus _status;
    private System.Threading.Timer? _heartbeatTimer;

    public ClientRegistryService(SettingsService settings, SessionInfoService session, ConnectionStatus? status = null)
    {
        _settings = settings;
        _session = session;
        _status = status ?? new ConnectionStatus();
    }

    public void StartHeartbeat()
    {
        WriteClientFile();
        _heartbeatTimer = new System.Threading.Timer(
            _ => WriteClientFile(),
            null,
            // Jitter so many clients started together don't hit the share in sync.
            TimeSpan.FromSeconds(HeartbeatSeconds) + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000)),
            TimeSpan.FromSeconds(HeartbeatSeconds));
    }

    private int HeartbeatSeconds => Math.Max(1, _settings.Settings.HeartbeatSeconds);

    /// <summary>
    /// Seconds without a heartbeat after which a client counts as offline. Never
    /// below the v1.1.0-rc.1 threshold (5 polling intervals), so clients that
    /// still write every polling interval keep showing up as online.
    /// </summary>
    public int OnlineThresholdSeconds => Math.Max(HeartbeatSeconds * 3, _settings.Settings.PollingSeconds * 5);

    public void StopHeartbeat()
    {
        _heartbeatTimer?.Dispose();
    }

    private void WriteClientFile()
    {
        try
        {
            var clientsPath = _settings.GetClientsPath();
            Directory.CreateDirectory(clientsPath);

            var info = _session.GetCurrentSession();
            info.LastSeen = DateTime.Now;

            var filePath = Path.Combine(clientsPath, $"{info.ClientId}.json");
            FileLockHelper.WriteAllTextSafe(filePath, JsonHelper.Serialize(info));
            if (_status.ReportSuccess())
                Log.Info("Cartella condivisa di nuovo raggiungibile (heartbeat)");
        }
        catch (Exception ex)
        {
            // Heartbeat failure is non-fatal; log once per outage, not every cycle.
            if (_status.ReportFailure("Heartbeat", ex))
                Log.Warn("Heartbeat sulla cartella condivisa fallito", ex);
        }
    }

    public List<ClientInfo> GetOnlineClients()
    {
        var clients = new List<ClientInfo>();
        var clientsPath = _settings.GetClientsPath();

        if (!Directory.Exists(clientsPath))
            return clients;

        var threshold = OnlineThresholdSeconds;

        foreach (var file in Directory.GetFiles(clientsPath, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var client = JsonHelper.Deserialize<ClientInfo>(json);
                if (client != null && client.IsOnline(threshold))
                    clients.Add(client);
            }
            catch (Exception ex)
            {
                Log.Warn($"File client illeggibile: '{file}'", ex);
            }
        }

        return clients;
    }

    /// <summary>
    /// Deletes presence files of clients not seen for <paramref name="maxAge"/>
    /// (PCs replaced or renamed, users who left) and returns how many were removed.
    /// A client that comes back simply writes its file again.
    /// </summary>
    public int RemoveStaleClients(TimeSpan maxAge)
    {
        var clientsPath = _settings.GetClientsPath();
        if (!Directory.Exists(clientsPath)) return 0;

        var removed = 0;
        foreach (var file in Directory.GetFiles(clientsPath, "*.json"))
        {
            try
            {
                var removedThis = false;
                // Re-read under the lock: the client may have just written a fresh heartbeat.
                FileLockHelper.WithLock(file, () =>
                {
                    if (!File.Exists(file)) return;
                    var client = JsonHelper.Deserialize<ClientInfo>(File.ReadAllText(file));
                    if (client == null || client.IsOnline((int)maxAge.TotalSeconds)) return;
                    File.Delete(file);
                    removedThis = true;
                });
                if (!removedThis) continue;
                FileLockHelper.DeleteLockFile(file);
                removed++;
            }
            catch (Exception ex)
            {
                Log.Warn($"Rimozione del client inattivo '{file}' non riuscita", ex);
            }
        }

        if (removed > 0)
            Log.Info($"Rimossi {removed} client inattivi da clients\\");
        return removed;
    }
}
