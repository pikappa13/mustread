using MustRead.App.Services;

namespace MustRead.App.Forms;

public class SettingsForm : Form
{
    private readonly SettingsService _settings;
    private readonly bool _showRestartWarning;

    private TextBox _serverPathBox = null!;
    private NumericUpDown _pollingSpin = null!;
    private CheckBox _enableSenderCheck = null!;
    private CheckBox _enableReceiverCheck = null!;
    private CheckBox _allowSendToAllCheck = null!;
    private CheckBox _autostartCheck = null!;
    private TextBox _appNameBox = null!;
    private Label _statusLabel = null!;

    private static readonly (string Title, string Body)[] HelpItems =
    {
        ("Percorso cartella condivisa",
            "Il percorso UNC della cartella di rete usata come backend, ad esempio:\n\\\\SERVER\\MustRead\n\n" +
            "Tutti i PC che usano MustRead devono puntare alla stessa cartella. " +
            "La cartella deve essere accessibile in lettura e scrittura."),

        ("Nome applicazione",
            "Il nome visualizzato nella tray icon e nelle finestre di MustRead. " +
            "Puoi personalizzarlo liberamente (es. \"Avvisi Aziendali\")."),

        ("Polling (secondi)",
            "Ogni quanti secondi il programma controlla se sono arrivati nuovi messaggi. " +
            "Valori bassi (1-3 s) = reattività alta ma più traffico di rete. " +
            "Valori alti (10-30 s) = meno traffico, notifica ritardata."),

        ("Abilita invio messaggi",
            "Se attivo, questo PC può aprire la schermata di invio e spedire messaggi. " +
            "Disattivalo sui PC che devono solo ricevere."),

        ("Abilita ricezione messaggi",
            "Se attivo, l'agent controlla periodicamente la cartella condivisa e mostra " +
            "le notifiche in arrivo. Disattivalo solo se questo PC non deve ricevere nulla."),

        ("Consenti invio a tutti",
            "Mostra il destinatario \"TUTTI\" nella lista di invio, che consente di inviare " +
            "un messaggio a tutti i client contemporaneamente. " +
            "Disattivalo per limitare gli invii a utenti/computer specifici."),

        ("Avvia automaticamente con Windows",
            "Se attivo, MustRead viene avviato automaticamente al login dell'utente corrente " +
            "tramite una voce nel registro di sistema:\n" +
            "HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run\n\n" +
            "Non richiede privilegi di amministratore. " +
            "L'autostart vale solo per l'utente che salva questa impostazione."),
    };

    public SettingsForm(SettingsService settings, bool showRestartWarning = false)
    {
        _settings = settings;
        _showRestartWarning = showRestartWarning;
        InitializeComponent();
        PopulateFields();
    }

    private void InitializeComponent()
    {
        Text = "MustRead — Impostazioni";
        Size = new Size(540, 430);
        MinimumSize = new Size(460, 390);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        Font = new Font("Segoe UI", 10f);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 2,
            RowCount = 10
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        for (int i = 0; i < 10; i++)
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        int row = 0;

        // ── Server path (textbox + browse button) ──────────────────────────
        layout.Controls.Add(MakeLabel("Percorso cartella condivisa:"), 0, row);
        var pathPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        _serverPathBox = new TextBox { PlaceholderText = @"\\SERVER\MustRead", Width = 270 };
        var btnBrowse = new Button
        {
            Text = "...",
            Width = 34,
            Height = _serverPathBox.Height + 4,
            Margin = new Padding(4, 0, 0, 0),
            Cursor = Cursors.Hand
        };
        btnBrowse.Click += Browse_Click;
        pathPanel.Controls.Add(_serverPathBox);
        pathPanel.Controls.Add(btnBrowse);
        layout.Controls.Add(pathPanel, 1, row++);

        // ── App name ────────────────────────────────────────────────────────
        layout.Controls.Add(MakeLabel("Nome applicazione:"), 0, row);
        _appNameBox = new TextBox { Dock = DockStyle.Fill };
        layout.Controls.Add(_appNameBox, 1, row++);

        // ── Polling ─────────────────────────────────────────────────────────
        layout.Controls.Add(MakeLabel("Polling (secondi):"), 0, row);
        _pollingSpin = new NumericUpDown { Minimum = 1, Maximum = 60, Value = 3, Width = 80 };
        layout.Controls.Add(_pollingSpin, 1, row++);

        // ── Checkboxes ──────────────────────────────────────────────────────
        layout.Controls.Add(MakeLabel(""), 0, row);
        _enableSenderCheck = new CheckBox { Text = "Abilita invio messaggi (solo per admin)", AutoSize = true };
        layout.Controls.Add(_enableSenderCheck, 1, row++);

        layout.Controls.Add(MakeLabel(""), 0, row);
        _enableReceiverCheck = new CheckBox { Text = "Abilita ricezione messaggi", AutoSize = true };
        layout.Controls.Add(_enableReceiverCheck, 1, row++);

        layout.Controls.Add(MakeLabel(""), 0, row);
        _allowSendToAllCheck = new CheckBox { Text = "Consenti invio a tutti", AutoSize = true };
        layout.Controls.Add(_allowSendToAllCheck, 1, row++);

        // ── Separator ───────────────────────────────────────────────────────
        var sep = new Panel { Dock = DockStyle.Fill, Height = 1, BackColor = Color.LightGray, Margin = new Padding(0, 6, 0, 6) };
        layout.Controls.Add(sep, 0, row);
        layout.SetColumnSpan(sep, 2);
        row++;

        // ── Autostart ───────────────────────────────────────────────────────
        layout.Controls.Add(MakeLabel(""), 0, row);
        _autostartCheck = new CheckBox { Text = "Avvia automaticamente con Windows", AutoSize = true };
        layout.Controls.Add(_autostartCheck, 1, row++);

        // ── Buttons (? | Annulla | Salva) ───────────────────────────────────
        var btnPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill
        };
        var btnSave = new Button
        {
            Text = "Salva",
            Width = 90,
            Height = 34,
            BackColor = Color.FromArgb(0, 122, 204),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btnSave.FlatAppearance.BorderSize = 0;
        btnSave.Click += Save_Click;

        var btnCancel = new Button { Text = "Annulla", Width = 90, Height = 34 };
        btnCancel.Click += (_, _) => Close();

        var btnHelp = new Button
        {
            Text = "?",
            Width = 34,
            Height = 34,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnHelp.Click += Help_Click;

        btnPanel.Controls.Add(btnSave);
        btnPanel.Controls.Add(btnCancel);
        btnPanel.Controls.Add(btnHelp);
        layout.Controls.Add(new Label(), 0, row);
        layout.Controls.Add(btnPanel, 1, row++);

        // ── Status ──────────────────────────────────────────────────────────
        _statusLabel = new Label
        {
            AutoSize = true,
            ForeColor = Color.Green,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 4, 0, 0)
        };
        layout.Controls.Add(_statusLabel, 0, row);
        layout.SetColumnSpan(_statusLabel, 2);

        Controls.Add(layout);
        AcceptButton = btnSave;
        CancelButton = btnCancel;
        Utils.UiHelper.ApplyIcon(this);
        Utils.UiHelper.AddFooter(this);
    }

    private void Browse_Click(object? sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "Seleziona la cartella condivisa MustRead",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
            SelectedPath = _serverPathBox.Text.Trim()
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            _serverPathBox.Text = dlg.SelectedPath;
    }

    private void Help_Click(object? sender, EventArgs e)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var (title, body) in HelpItems)
        {
            sb.AppendLine($"▶ {title}");
            sb.AppendLine(body);
            sb.AppendLine();
        }

        using var dlg = new Form
        {
            Text = "MustRead — Guida alle impostazioni",
            Size = new Size(560, 520),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            Font = new Font("Segoe UI", 10f)
        };
        var rtb = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            BackColor = SystemColors.Control,
            Text = sb.ToString().TrimEnd(),
            ScrollBars = RichTextBoxScrollBars.Vertical
        };
        var btnClose = new Button { Text = "Chiudi", Dock = DockStyle.Bottom, Height = 36, DialogResult = DialogResult.Cancel };
        dlg.Controls.Add(rtb);
        dlg.Controls.Add(btnClose);
        dlg.CancelButton = btnClose;
        dlg.ShowDialog(this);
    }

    private void PopulateFields()
    {
        _serverPathBox.Text = _settings.Settings.ServerPath;
        _appNameBox.Text = _settings.Settings.ApplicationName;
        _pollingSpin.Value = Math.Clamp(_settings.Settings.PollingSeconds, 1, 60);
        _enableSenderCheck.Checked = _settings.Settings.EnableSender;
        _enableReceiverCheck.Checked = _settings.Settings.EnableReceiver;
        _allowSendToAllCheck.Checked = _settings.Settings.AllowSendToAll;
        _autostartCheck.Checked = AutostartService.IsEnabled();
    }

    private void Save_Click(object? sender, EventArgs e)
    {
        var path = _serverPathBox.Text.Trim();
        if (string.IsNullOrEmpty(path))
        {
            _statusLabel.Text = "Il percorso cartella condivisa è obbligatorio.";
            _statusLabel.ForeColor = Color.OrangeRed;
            return;
        }

        _settings.Settings.ServerPath = path;
        _settings.Settings.ApplicationName = string.IsNullOrWhiteSpace(_appNameBox.Text) ? "MustRead" : _appNameBox.Text.Trim();
        _settings.Settings.PollingSeconds = (int)_pollingSpin.Value;
        _settings.Settings.EnableSender = _enableSenderCheck.Checked;
        _settings.Settings.EnableReceiver = _enableReceiverCheck.Checked;
        _settings.Settings.AllowSendToAll = _allowSendToAllCheck.Checked;

        try
        {
            _settings.Save();

            // Applica autostart indipendentemente dalle altre impostazioni
            if (_autostartCheck.Checked)
                AutostartService.Enable(Application.ExecutablePath);
            else
                AutostartService.Disable();

            DialogResult = DialogResult.OK;
            if (_showRestartWarning)
            {
                _statusLabel.Text = "Impostazioni salvate. Riavviare MustRead per applicarle.";
                _statusLabel.ForeColor = Color.Green;
            }
            else
            {
                Close();
            }
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Errore salvataggio: {ex.Message}";
            _statusLabel.ForeColor = Color.OrangeRed;
        }
    }

    private static Label MakeLabel(string text) =>
        new() { Text = text, AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top, Padding = new Padding(0, 6, 8, 0) };
}
