using MustRead.App.Utils;

namespace MustRead.App.Services;

/// <summary>
/// Periodic housekeeping of the share, run only by sender instances (receivers
/// have no rights to delete inbox files): removes expired messages and the
/// presence files of clients gone for a long time.
/// </summary>
public class MaintenanceService
{
    public static readonly TimeSpan StaleClientAge = TimeSpan.FromDays(7);

    private readonly MessageSenderService _sender;
    private readonly ClientRegistryService _registry;
    private System.Threading.Timer? _timer;
    // 1 while a run is in progress: a slow share must not stack runs.
    private int _running;

    public MaintenanceService(MessageSenderService sender, ClientRegistryService registry)
    {
        _sender = sender;
        _registry = registry;
    }

    public void Start(TimeSpan firstRun, TimeSpan interval)
    {
        _timer = new System.Threading.Timer(_ => Run(), null, firstRun, interval);
    }

    public void Stop()
    {
        _timer?.Dispose();
    }

    public void Run()
    {
        if (Interlocked.Exchange(ref _running, 1) == 1) return;
        try
        {
            // Same authorization as sending: only senders clean up the share.
            if (!_sender.IsAuthorized()) return;

            _sender.CleanupExpiredMessages();
            _registry.RemoveStaleClients(StaleClientAge);
        }
        catch (Exception ex)
        {
            Log.Warn("Manutenzione della cartella condivisa non riuscita", ex);
        }
        finally
        {
            Interlocked.Exchange(ref _running, 0);
        }
    }
}
