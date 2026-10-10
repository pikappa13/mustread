using System.Diagnostics;
using System.Text;
using MustRead.App.Utils;

namespace MustRead.App.Services;

public record DiagnosticResult(string Name, DiagnosticLevel Level, string Detail);

public enum DiagnosticLevel { Ok, Warning, Error }

/// <summary>
/// "Verifica connessione": checks configuration, share reachability and the
/// permissions this client needs. The report never contains message contents
/// and is safe to paste into a public issue after removing server names.
/// </summary>
public class DiagnosticsService
{
    private readonly SettingsService _settings;
    private readonly SessionInfoService _session;
    private readonly HistoryService _history;
    private readonly MessageSenderService _sender;
    private readonly ConnectionStatus _status;

    public DiagnosticsService(SettingsService settings, SessionInfoService session, HistoryService history,
        MessageSenderService sender, ConnectionStatus status)
    {
        _settings = settings;
        _session = session;
        _history = history;
        _sender = sender;
        _status = status;
    }

    public List<DiagnosticResult> Run()
    {
        var results = new List<DiagnosticResult>();
        var s = _settings.Settings;

        if (string.IsNullOrWhiteSpace(s.ServerPath))
        {
            results.Add(new("Configurazione", DiagnosticLevel.Error, "Percorso della cartella condivisa non impostato."));
            return results;
        }
        results.Add(new("Configurazione", DiagnosticLevel.Ok,
            $"Polling {s.PollingSeconds}s, invio {(s.EnableSender ? "attivo" : "disattivo")}, ricezione {(s.EnableReceiver ? "attiva" : "disattiva")}."));

        var sw = Stopwatch.StartNew();
        bool reachable;
        try { reachable = Directory.Exists(s.ServerPath); }
        catch { reachable = false; }
        sw.Stop();

        if (!reachable)
        {
            results.Add(new("Cartella condivisa", DiagnosticLevel.Error,
                "Non raggiungibile: verificare rete, nome del server e permessi di lettura."));
            AddLocalChecks(results);
            return results;
        }
        results.Add(new("Cartella condivisa", sw.ElapsedMilliseconds > 1000 ? DiagnosticLevel.Warning : DiagnosticLevel.Ok,
            $"Raggiungibile, risposta in {sw.ElapsedMilliseconds} ms."));

        var client = _session.GetCurrentSession();
        CheckReadable(results, "Inbox utente", () => _settings.GetInboxUserPath(client.Domain, client.Username));
        CheckReadable(results, "Inbox computer", () => _settings.GetInboxComputerPath(client.ComputerName));
        CheckReadable(results, "Inbox tutti", _settings.GetInboxAllPath);
        CheckWritable(results, "Scrittura heartbeat (clients)", _settings.GetClientsPath());
        CheckWritable(results, "Scrittura storico (history)", Path.Combine(s.ServerPath, "history"));

        if (s.EnableSender)
            results.Add(CheckAuthorization());

        AddLocalChecks(results);
        return results;
    }

    private DiagnosticResult CheckAuthorization()
    {
        var path = _settings.GetConfigPath("authorized_senders.json");
        bool exists;
        try { exists = File.Exists(path); }
        catch { exists = false; }

        var authorized = _sender.IsAuthorized();
        if (!exists)
        {
            return _settings.Settings.RequireAuthorizedSenders
                ? new("Autorizzazione invio", DiagnosticLevel.Warning,
                    "authorized_senders.json assente e requireAuthorizedSenders attivo: nessuno può inviare.")
                : new("Autorizzazione invio", DiagnosticLevel.Warning,
                    "authorized_senders.json assente: chiunque abbia l'invio abilitato può inviare.");
        }

        return authorized
            ? new("Autorizzazione invio", DiagnosticLevel.Ok, "Utente corrente presente in authorized_senders.json.")
            : new("Autorizzazione invio", DiagnosticLevel.Warning,
                "Utente corrente non autorizzato, oppure authorized_senders.json è illeggibile o malformato.");
    }

    private void AddLocalChecks(List<DiagnosticResult> results)
    {
        var pending = _history.PendingCount;
        results.Add(new("Conferme in coda", pending == 0 ? DiagnosticLevel.Ok : DiagnosticLevel.Warning,
            pending == 0 ? "Nessuna: lo storico sulla share è aggiornato."
                         : $"{pending} record in attesa di essere scritti sulla share."));

        var lastOk = _status.LastSuccess;
        var lastErr = _status.LastError;
        results.Add(new("Ultima operazione riuscita", lastOk == null ? DiagnosticLevel.Warning : DiagnosticLevel.Ok,
            lastOk?.ToString("dd/MM/yyyy HH:mm:ss") ?? "nessuna"));
        if (_status.IsOnline == false && lastErr != null)
            results.Add(new("Ultimo errore", DiagnosticLevel.Error, lastErr));

        results.Add(new("Log locale", DiagnosticLevel.Ok, Log.Directory));
    }

    private static void CheckReadable(List<DiagnosticResult> results, string name, Func<string> getPath)
    {
        try
        {
            var path = getPath();
            if (!Directory.Exists(path))
            {
                results.Add(new(name, DiagnosticLevel.Ok, "Cartella non ancora creata (nessun messaggio ricevuto)."));
                return;
            }
            Directory.GetFiles(path, "*.json");
            results.Add(new(name, DiagnosticLevel.Ok, "Leggibile."));
        }
        catch (Exception ex)
        {
            results.Add(new(name, DiagnosticLevel.Error, $"Non leggibile: {ex.GetType().Name}."));
        }
    }

    private static void CheckWritable(List<DiagnosticResult> results, string name, string dir)
    {
        var probe = Path.Combine(dir, $".mustread-probe-{Guid.NewGuid():N}.tmp");
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(probe, "probe");
            File.Delete(probe);
            results.Add(new(name, DiagnosticLevel.Ok, "Scrivibile."));
        }
        catch (Exception ex)
        {
            results.Add(new(name, DiagnosticLevel.Error, $"Non scrivibile: {ex.GetType().Name}."));
            try { if (File.Exists(probe)) File.Delete(probe); } catch { /* best effort */ }
        }
    }

    public static string Format(IEnumerable<DiagnosticResult> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"MustRead {typeof(DiagnosticsService).Assembly.GetName().Version} — {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
        sb.AppendLine($"Windows {Environment.OSVersion.Version}, sessione {Environment.GetEnvironmentVariable("SESSIONNAME") ?? "?"}");
        sb.AppendLine();
        foreach (var r in results)
        {
            var mark = r.Level switch
            {
                DiagnosticLevel.Ok => "[OK]  ",
                DiagnosticLevel.Warning => "[ATT] ",
                _ => "[ERR] "
            };
            sb.AppendLine($"{mark}{r.Name}: {r.Detail}");
        }
        return sb.ToString();
    }
}
