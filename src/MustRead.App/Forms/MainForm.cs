using MustRead.App.Models;
using MustRead.App.Services;

namespace MustRead.App.Forms;

/// <summary>
/// Invisible background form that hosts the tray icon.
/// Shows SenderForm on double-click / menu and processes incoming alerts.
/// </summary>
public class MainForm : Form
{
    private readonly SettingsService _settings;
    private readonly ClientRegistryService _registry;
    private readonly MessageSenderService _sender;
    private readonly MessageReceiverService _receiver;

    private NotifyIcon _trayIcon = null!;
    private SenderForm? _senderForm;

    // Queue for messages that arrive while an alert is already showing
    private readonly Queue<AlertMessage> _pendingAlerts = new();
    private bool _alertShowing = false;

    public MainForm(
        SettingsService settings,
        ClientRegistryService registry,
        MessageSenderService sender,
        MessageReceiverService receiver)
    {
        _settings = settings;
        _registry = registry;
        _sender = sender;
        _receiver = receiver;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = _settings.Settings.ApplicationName;
        Size = new Size(1, 1);
        ShowInTaskbar = false;
        WindowState = FormWindowState.Minimized;
        Opacity = 0;

        var menu = new ContextMenuStrip();
        if (_settings.Settings.EnableSender)
        {
            var sendItem = new ToolStripMenuItem("Invia messaggio", null, OpenSenderForm);
            menu.Items.Add(sendItem);
            menu.Items.Add(new ToolStripSeparator());
        }

        var historyItem = new ToolStripMenuItem("Storico", null, OpenHistory);
        var settingsItem = new ToolStripMenuItem("Impostazioni", null, OpenSettings);
        var exitItem = new ToolStripMenuItem("Esci", null, ExitApp);
        menu.Items.Add(historyItem);
        menu.Items.Add(settingsItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        _trayIcon = new NotifyIcon
        {
            Text = _settings.Settings.ApplicationName,
            Icon = Utils.UiHelper.LoadAppIcon(),
            ContextMenuStrip = menu,
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => OpenSenderForm(null, EventArgs.Empty);

        _receiver.MessageReceived += OnMessageReceived;
    }

    private void OpenSenderForm(object? s, EventArgs e)
    {
        if (!_settings.Settings.EnableSender) return;
        if (!_sender.IsAuthorized())
        {
            MessageBox.Show(
                "Non sei autorizzato a inviare messaggi.",
                _settings.Settings.ApplicationName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (_senderForm == null || _senderForm.IsDisposed)
        {
            _senderForm = new SenderForm(_settings, _sender, _registry);
            _senderForm.Show();
        }
        else
        {
            _senderForm.BringToFront();
            _senderForm.Focus();
        }
    }

    private void OpenHistory(object? s, EventArgs e)
    {
        new HistoryForm(_settings).Show();
    }

    private void OpenSettings(object? s, EventArgs e)
    {
        Program.OpenSettingsElevated();
    }

    private void ExitApp(object? s, EventArgs e)
    {
        _trayIcon.Visible = false;
        _receiver.Stop();
        _registry.StopHeartbeat();
        Application.Exit();
    }

    private void OnMessageReceived(AlertMessage msg)
    {
        // Marshal to UI thread
        if (InvokeRequired)
        {
            Invoke(() => OnMessageReceived(msg));
            return;
        }

        _pendingAlerts.Enqueue(msg);
        ShowNextAlert();
    }

    private void ShowNextAlert()
    {
        if (_alertShowing || _pendingAlerts.Count == 0) return;

        var msg = _pendingAlerts.Dequeue();
        _alertShowing = true;

        var alert = new AlertForm(msg, _receiver);
        alert.FormClosed += (_, _) =>
        {
            _alertShowing = false;
            ShowNextAlert();
        };
        alert.Show();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        Hide();

        try
        {
            if (_settings.Settings.EnableReceiver)
                _receiver.Start();

            _registry.StartHeartbeat();
        }
        catch (Exception ex)
        {
            Utils.Log.Error("Avvio di ricezione/heartbeat fallito", ex);
            MessageBox.Show(
                $"Errore durante l'avvio:\n\n{ex.Message}\n\nVerifica il percorso della cartella condivisa nelle Impostazioni.",
                _settings.Settings.ApplicationName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _trayIcon?.Dispose();
        }
        base.Dispose(disposing);
    }
}
