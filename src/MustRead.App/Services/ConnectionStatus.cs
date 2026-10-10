namespace MustRead.App.Services;

/// <summary>
/// Tracks whether the last operations against the share succeeded, so the tray
/// can tell the user "connected" from "share unreachable". Thread-safe.
/// </summary>
public class ConnectionStatus
{
    private readonly object _sync = new();
    private DateTime? _lastSuccess;
    private DateTime? _lastFailure;
    private string? _lastError;

    public DateTime? LastSuccess { get { lock (_sync) return _lastSuccess; } }
    public DateTime? LastFailure { get { lock (_sync) return _lastFailure; } }
    public string? LastError { get { lock (_sync) return _lastError; } }

    /// <summary>Null until the first operation completes.</summary>
    public bool? IsOnline
    {
        get
        {
            lock (_sync)
            {
                if (_lastSuccess == null && _lastFailure == null) return null;
                if (_lastFailure == null) return true;
                return _lastSuccess != null && _lastSuccess >= _lastFailure;
            }
        }
    }

    /// <summary>Returns true when this success ends an offline period.</summary>
    public bool ReportSuccess()
    {
        lock (_sync)
        {
            var wasOffline = _lastFailure != null && (_lastSuccess == null || _lastFailure > _lastSuccess);
            _lastSuccess = DateTime.Now;
            return wasOffline;
        }
    }

    /// <summary>
    /// Returns true when this failure starts an offline period, so callers can
    /// log once per outage instead of once per poll.
    /// </summary>
    public bool ReportFailure(string operation, Exception? ex = null)
    {
        lock (_sync)
        {
            var wasOnline = _lastFailure == null || (_lastSuccess != null && _lastSuccess >= _lastFailure);
            _lastFailure = DateTime.Now;
            _lastError = ex == null ? operation : $"{operation}: {ex.Message}";
            return wasOnline;
        }
    }
}
