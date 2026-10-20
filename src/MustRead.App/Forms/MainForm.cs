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
    private readonly HistoryService _history;
    private readonly ConnectionStatus _status;
    private readonly DiagnosticsService _diagnostics;
    private readonly MaintenanceService _maintenance;

    private NotifyIcon _trayIcon = null!;
    private ToolStripMenuItem _autostartItem = null!;
    private System.Windows.Forms.Timer _statusTimer = null!;
    private bool? _lastOnline;
    private SenderForm? _senderForm;

    // Queue for messages that arrive while an alert is already showing
    private readonly Queue<AlertMessage> _pendingAlerts = new();
    private bool _alertShowing = false;

    public MainForm(
        SettingsService settings,
        ClientRegistryService registry,
        MessageSenderService sender,
        MessageReceiverService receiver,
        HistoryService history,
        ConnectionStatus status,
        DiagnosticsService diagnostics,
        MaintenanceService maintenance)
    {
        _settings = settings;
        _registry = registry;
        _sender = sender;
        _receiver = receiver;
        _history = history;
        _status = status;
        _diagnostics = diagnostics;
        _maintenance = maintenance;
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
        var diagnosticsItem = new ToolStripMenuItem("Verifica connessione...", null, OpenDiagnostics);
        _autostartItem = new ToolStripMenuItem("Avvia con Windows", null, ToggleAutostart)
        {
            Checked = AutostartService.IsEnabled()
        };
        var settingsItem = new ToolStripMenuItem("Impostazioni", null, OpenSettings);
        var exitItem = new ToolStripMenuItem("Esci", null, ExitApp);
        menu.Items.Add(historyItem);
        menu.Items.Add(diagnosticsItem);
        menu.Items.Add(_autostartItem);
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

        _statusTimer = new System.Windows.Forms.Timer { Interval = 5000 };
        _statusTimer.Tick += (_, _) => UpdateTrayStatus();
        _statusTimer.Start();
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

    private void OpenDiagnostics(object? s, EventArgs e)
    {
        Cursor.Current = Cursors.WaitCursor;
        var report = DiagnosticsService.Format(_diagnostics.Run());
        Cursor.Current = Cursors.Default;
        using var dlg = new DiagnosticsForm(_settings.Settings.ApplicationName, report);
        dlg.ShowDialog();
    }

    private void ToggleAutostart(object? s, EventArgs e)
    {
        try
        {
            if (_autostartItem.Checked)
                AutostartService.Disable();
            else
                AutostartService.Enable(Application.ExecutablePath);
            _autostartItem.Checked = AutostartService.IsEnabled();
        }
        catch (Exception ex)
        {
            Utils.Log.Error("Modifica avvio automatico fallita", ex);
            MessageBox.Show($"Impossibile modificare l'avvio automatico:\n{ex.Message}",
                _settings.Settings.ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void UpdateTrayStatus()
    {
        var online = _status.IsOnline;
        var pending = _history.PendingCount;
        var state = online switch
        {
            true => "connesso",
            false => "cartella condivisa non raggiungibile",
            null => "in avvio"
        };
        var text = $"{_settings.Settings.ApplicationName} — {state}";
        if (pending > 0)
            text += $" · {pending} conferme da sincronizzare";
        // NotifyIcon.Text is limited to 127 characters.
        _trayIcon.Text = text.Length > 127 ? text[..127] : text;

        if (online == false && _lastOnline == true)
        {
            _trayIcon.ShowBalloonTip(5000, _settings.Settings.ApplicationName,
                "Cartella condivisa non raggiungibile: i messaggi potrebbero arrivare in ritardo.",
                ToolTipIcon.Warning);
        }
        _lastOnline = online;
    }

    private void OpenSettings(object? s, EventArgs e)
    {
        Program.OpenSettingsElevated();
    }

    private void ExitApp(object? s, EventArgs e)
    {
        _trayIcon.Visible = false;
        _statusTimer.Stop();
        _receiver.Stop();
        _registry.StopHeartbeat();
        _history.StopBackgroundFlush();
        _maintenance.Stop();
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
            // Retries records queued while the share was offline (or from a previous run).
            _history.StartBackgroundFlush(TimeSpan.FromSeconds(30));
            if (_settings.Settings.EnableSender)
                _maintenance.Start(TimeSpan.FromMinutes(1), TimeSpan.FromHours(1));
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
            _statusTimer?.Dispose();
        }
        base.Dispose(disposing);
    }
}
