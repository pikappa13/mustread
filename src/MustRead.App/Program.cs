using MustRead.App.Forms;
using MustRead.App.Services;

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
            MessageBox.Show(
                "MustRead è già in esecuzione.",
                "MustRead",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, ex) =>
            MessageBox.Show(ex.Exception.ToString(), "MustRead — Errore imprevisto",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
            MessageBox.Show(ex.ExceptionObject?.ToString(), "MustRead — Errore critico",
                MessageBoxButtons.OK, MessageBoxIcon.Error);

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

        var session = new SessionInfoService();
        var history = new HistoryService(settings);
        var registry = new ClientRegistryService(settings, session);
        var sender = new MessageSenderService(settings, session, history);
        var receiver = new MessageReceiverService(settings, session, history);

        Application.Run(new MainForm(settings, registry, sender, receiver));
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
