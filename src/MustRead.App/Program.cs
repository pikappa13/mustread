using MustRead.App.Forms;
using MustRead.App.Services;
using MustRead.App.Utils;

namespace MustRead.App;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // Modalità impostazioni: lanciata con UAC elevation da un'istanza già in esecuzione
        if (args.Contains("--settings"))
        {
            RunSettingsOnly();
            return;
        }

        using var mutex = new Mutex(true, "MustRead_SingleInstance", out bool isNewInstance);
        if (!isNewInstance)
        {
            // Started by the Run key / installer while already running (e.g. both a
            // per-user and a machine-wide autostart entry): nothing to tell the user.
            if (args.Contains(AutostartService.AutostartArgument))
                return;

            MessageBox.Show(
                "MustRead è già in esecuzione.",
                "MustRead",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, ex) =>
        {
            Log.Error("Eccezione non gestita nel thread UI", ex.Exception);
            MessageBox.Show(ex.Exception.ToString(), "MustRead — Errore imprevisto",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
        {
            Log.Error("Eccezione critica non gestita", ex.ExceptionObject as Exception);
            MessageBox.Show(ex.ExceptionObject?.ToString(), "MustRead — Errore critico",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        };

        Log.Info($"MustRead {Application.ProductVersion} avviato");

        var settings = new SettingsService();
        settings.Load();

        // First-run: apre impostazioni se serverPath non configurato
        if (string.IsNullOrWhiteSpace(settings.Settings.ServerPath))
        {
            using var setup = new SettingsForm(settings);
            setup.Text = "MustRead — Configurazione iniziale";
            if (setup.ShowDialog() != DialogResult.OK || string.IsNullOrWhiteSpace(settings.Settings.ServerPath))
                return;
        }

        var status = new ConnectionStatus();
        var session = new SessionInfoService();
        var history = new HistoryService(settings, status);
        var registry = new ClientRegistryService(settings, session, status);
        var sender = new MessageSenderService(settings, session, history);
        var receiver = new MessageReceiverService(settings, session, history, status);
        var diagnostics = new DiagnosticsService(settings, session, history, sender, status);

        Application.Run(new MainForm(settings, registry, sender, receiver, history, status, diagnostics));
    }

    private static void RunSettingsOnly()
    {
        var settings = new SettingsService();
        settings.Load();
        using var form = new SettingsForm(settings, showRestartWarning: true);
        form.Text = "MustRead — Impostazioni (Amministratore)";
        Application.Run(form);
    }

    /// <summary>
    /// Rilancia MustRead.exe --settings con richiesta UAC (runas).
    /// Restituisce true se il processo è stato avviato, false se l'utente ha annullato l'UAC.
    /// </summary>
    public static bool OpenSettingsElevated()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = Application.ExecutablePath,
                Arguments = "--settings",
                Verb = "runas", // forza UAC
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // L'utente ha premuto "Annulla" sulla finestra UAC
            return false;
        }
    }
}
