using MustRead.App.Models;
using MustRead.App.Utils;

namespace MustRead.App.Services;

/// <summary>
/// Writes sent/shown/read records to the JSONL files on the share.
/// Every record is first saved in a local per-user outbox and only removed once
/// it has been appended to the share, so a read confirmation survives an
/// offline share, a crash or the app being closed. Delivery is at-least-once:
/// a crash between append and delete can produce a duplicate line.
/// </summary>
public class HistoryService
{
    private readonly SettingsService _settings;
    private readonly ConnectionStatus? _status;
    private readonly object _flushSync = new();
    private System.Threading.Timer? _flushTimer;

    public HistoryService(SettingsService settings, ConnectionStatus? status = null)
    {
        _settings = settings;
        _status = status;
    }

    public void RecordSent(AlertMessage msg, ClientInfo sender)
    {
        var item = new SentHistoryItem
        {
            MessageId = msg.MessageId,
            Sender = $@"{sender.Domain}\{sender.Username}",
            SenderComputer = sender.ComputerName,
            TargetType = msg.TargetType,
            Target = msg.Target,
            Body = msg.Body,
            SentAt = DateTime.Now
        };
        Append("sent.jsonl", item);
    }

    public void RecordShown(string messageId, ClientInfo receiver)
    {
        var item = new ShownHistoryItem
        {
            MessageId = messageId,
            ShownTo = $@"{receiver.Domain}\{receiver.Username}",
            ShownOnComputer = receiver.ComputerName,
            SessionType = receiver.SessionType,
            ShownAt = DateTime.Now
        };
        Append("shown.jsonl", item);
    }

    /// <param name="flush">
    /// False queues the record locally without touching the share; call
    /// <see cref="Flush"/> later (e.g. off the UI thread).
    /// </param>
    public void RecordRead(string messageId, ClientInfo receiver, bool flush = true)
    {
        var item = new ReadHistoryItem
        {
            MessageId = messageId,
            ReadBy = $@"{receiver.Domain}\{receiver.Username}",
            ReadOnComputer = receiver.ComputerName,
            SessionType = receiver.SessionType,
            ReadAt = DateTime.Now
        };
        Append("read.jsonl", item, flush);
    }

    /// <summary>Number of records still waiting to reach the share.</summary>
    public int PendingCount
    {
        get
        {
            try
            {
                var dir = _settings.GetLocalOutboxPath();
                return Directory.Exists(dir) ? Directory.GetFiles(dir, "*.json").Length : 0;
            }
            catch
            {
                return 0;
            }
        }
    }

    /// <summary>Retries the outbox periodically until stopped.</summary>
    public void StartBackgroundFlush(TimeSpan interval)
    {
        _flushTimer = new System.Threading.Timer(_ => Flush(), null, TimeSpan.Zero, interval);
    }

    public void StopBackgroundFlush()
    {
        _flushTimer?.Dispose();
    }

    private void Append<T>(string fileName, T item, bool flush = true)
    {
        var line = JsonHelper.Serialize(item)
            .Replace(Environment.NewLine, " ")
            .Replace("\n", " ");

        try
        {
            Enqueue(fileName, line);
        }
        catch (Exception ex)
        {
            // Local disk failure: fall back to a direct write so the record is not lost.
            Log.Error($"Coda locale non disponibile per {fileName}, scrivo direttamente", ex);
            try
            {
                FileLockHelper.AppendLine(_settings.GetHistoryPath(fileName), line);
            }
            catch (Exception ex2)
            {
                Log.Error($"Scrittura storico {fileName} fallita", ex2);
            }
            return;
        }

        if (flush)
            Flush();
    }

    private void Enqueue(string fileName, string line)
    {
        var dir = _settings.GetLocalOutboxPath();
        Directory.CreateDirectory(dir);
        // Ticks first so a plain name sort preserves the original order.
        var path = Path.Combine(dir, $"{DateTime.UtcNow.Ticks:D20}-{Guid.NewGuid():N}.json");
        var entry = new OutboxEntry { File = fileName, Line = line };
        FileLockHelper.WriteAtomic(path, JsonHelper.Serialize(entry));
    }

    /// <summary>
    /// Appends queued records to the share in order. Stops at the first failure
    /// so the order is kept; the next flush retries from there.
    /// </summary>
    public void Flush()
    {
        // In-process guard plus a per-user file lock: another instance of the same
        // user (second RDP session) must not append the same entries twice.
        if (!Monitor.TryEnter(_flushSync)) return;
        try
        {
            var dir = _settings.GetLocalOutboxPath();
            if (!Directory.Exists(dir)) return;

            FileLockHelper.WithLock(Path.Combine(dir, "flush"), () =>
            {
                foreach (var file in Directory.GetFiles(dir, "*.json").OrderBy(f => f, StringComparer.Ordinal))
                {
                    if (!TryFlushEntry(file)) return;
                }
            });
        }
        catch (Exception ex)
        {
            Log.Warn("Svuotamento coda storico interrotto", ex);
        }
        finally
        {
            Monitor.Exit(_flushSync);
        }
    }

    private bool TryFlushEntry(string file)
    {
        OutboxEntry? entry;
        try
        {
            entry = JsonHelper.Deserialize<OutboxEntry>(File.ReadAllText(file));
        }
        catch (Exception ex)
        {
            Log.Warn($"Voce di coda illeggibile, spostata: '{file}'", ex);
            TryMove(file, file + ".corrupt");
            return true;
        }

        if (entry == null || !IsKnownHistoryFile(entry.File))
        {
            Log.Warn($"Voce di coda non valida, spostata: '{file}'");
            TryMove(file, file + ".corrupt");
            return true;
        }

        try
        {
            FileLockHelper.AppendLine(_settings.GetHistoryPath(entry.File), entry.Line);
            _status?.ReportSuccess();
        }
        catch (Exception ex)
        {
            if (_status?.ReportFailure("Scrittura storico", ex) ?? true)
                Log.Warn($"Storico {entry.File} non raggiungibile, riprovo più tardi", ex);
            return false;
        }

        try
        {
            File.Delete(file);
        }
        catch (Exception ex)
        {
            // Will be appended again on the next flush (at-least-once).
            Log.Warn($"Voce di coda inviata ma non eliminata: '{file}'", ex);
        }
        return true;
    }

    private static bool IsKnownHistoryFile(string file)
        => file is "sent.jsonl" or "shown.jsonl" or "read.jsonl";

    private static void TryMove(string from, string to)
    {
        try { File.Move(from, to, overwrite: true); }
        catch { /* best effort */ }
    }

    private class OutboxEntry
    {
        public string File { get; set; } = string.Empty;
        public string Line { get; set; } = string.Empty;
    }
}
