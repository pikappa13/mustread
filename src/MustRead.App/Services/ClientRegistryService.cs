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
            TimeSpan.FromSeconds(_settings.Settings.PollingSeconds) + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000)),
            TimeSpan.FromSeconds(_settings.Settings.PollingSeconds));
    }

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

        var threshold = _settings.Settings.PollingSeconds * 5;

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
}
