using System.Runtime.InteropServices;
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
        // End users get a short message; the details (type and message) go to the log.
        Application.ThreadException += (_, ex) =>
        {
            Log.Error("Eccezione non gestita nel thread UI", ex.Exception);
            MessageBox.Show(
                $"Si è verificato un errore imprevisto. MustRead continua a funzionare.\n\nDettagli nel log: {Log.Directory}",
                "MustRead — Errore imprevisto", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };
        // The process is terminating: no dialog, so Windows can restart it right
        // away (see RegisterForRestart) instead of waiting for a click.
        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
            Log.Error("Eccezione critica non gestita, MustRead verrà riavviato", ex.ExceptionObject as Exception);

        RegisterForRestart();

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
        var maintenance = new MaintenanceService(sender, registry);

        Application.Run(new MainForm(settings, registry, sender, receiver, history, status, diagnostics, maintenance));
    }

    private const int RestartNoReboot = 8;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int RegisterApplicationRestart(string? commandLine, int flags);

    /// <summary>
    /// Asks Windows Error Reporting to restart MustRead if it crashes or hangs
    /// (after it has run for at least 60 seconds), so a crash does not leave the
    /// user without alerts until the next logon. Not after a reboot: autostart
    /// already covers that. The autostart argument keeps a restarted duplicate quiet.
    /// </summary>
    private static void RegisterForRestart()
    {
        try
        {
            var hr = RegisterApplicationRestart(AutostartService.AutostartArgument, RestartNoReboot);
            if (hr != 0)
                Log.Warn($"Registrazione del riavvio automatico non riuscita (HRESULT 0x{hr:X8})");
        }
        catch (Exception ex)
        {
            Log.Warn("Registrazione del riavvio automatico non riuscita", ex);
        }
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
