using MustRead.App.Models;
using MustRead.App.Services;

namespace MustRead.App.Forms;

public class SenderForm : Form
{
    private readonly SettingsService _settings;
    private readonly MessageSenderService _sender;
    private readonly ClientRegistryService _registry;

    private TextBox _searchBox = null!;
    private CheckedListBox _recipientList = null!;
    private TextBox _bodyBox = null!;
    private Button _sendButton = null!;
    private Label _statusLabel = null!;
    private Button _refreshButton = null!;

    private List<RecipientItem> _allRecipients = new();

    public SenderForm(SettingsService settings, MessageSenderService sender, ClientRegistryService registry)
    {
        _settings = settings;
        _sender = sender;
        _registry = registry;
        InitializeComponent();
        LoadRecipients();
    }

    private void InitializeComponent()
    {
        Text = _settings.Settings.ApplicationName;
        Size = new Size(560, 620);
        MinimumSize = new Size(400, 500);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(245, 245, 245);
        Font = new Font("Segoe UI", 10f);

        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            RowCount = 7,
            ColumnCount = 1
        };
        mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 45f));
        mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 30f));
        mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // Search + toolbar buttons
        var searchPanel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Width = 560 };
        _searchBox = new TextBox { Width = 260, PlaceholderText = "Cerca destinatario..." };
        _searchBox.TextChanged += (_, _) => FilterRecipients();
        _refreshButton = new Button { Text = "Aggiorna", AutoSize = true };
        _refreshButton.Click += (_, _) => LoadRecipients();
        var settingsButton = new Button { Text = "Impostazioni", AutoSize = true };
        settingsButton.Click += OpenSettings_Click;
        searchPanel.Controls.Add(_searchBox);
        searchPanel.Controls.Add(_refreshButton);
        searchPanel.Controls.Add(settingsButton);

        // Recipients label
        var recipLabel = new Label { Text = "Destinatari online:", AutoSize = true, Padding = new Padding(0, 8, 0, 2) };

        // Recipient list
        _recipientList = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true };

        // Message label
        var msgLabel = new Label { Text = "Messaggio:", AutoSize = true, Padding = new Padding(0, 8, 0, 2) };

        // Body
        _bodyBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            PlaceholderText = "Scrivi il messaggio qui..."
        };

        // Send button
        _sendButton = new Button
        {
            Text = "INVIA",
            Height = 44,
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(0, 122, 204),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _sendButton.FlatAppearance.BorderSize = 0;
        _sendButton.Click += SendButton_Click;

        // Status
        _statusLabel = new Label
        {
            Text = string.Empty,
            AutoSize = true,
            ForeColor = Color.Green,
            Padding = new Padding(0, 4, 0, 0)
        };

        mainPanel.Controls.Add(searchPanel, 0, 0);
        mainPanel.Controls.Add(recipLabel, 0, 1);
        mainPanel.Controls.Add(_recipientList, 0, 2);
        mainPanel.Controls.Add(msgLabel, 0, 3);
        mainPanel.Controls.Add(_bodyBox, 0, 4);
        mainPanel.Controls.Add(_sendButton, 0, 5);
        mainPanel.Controls.Add(_statusLabel, 0, 6);

        Controls.Add(mainPanel);
        Utils.UiHelper.ApplyIcon(this);
        Utils.UiHelper.AddFooter(this);
    }

    private void OpenSettings_Click(object? sender, EventArgs e)
    {
        Program.OpenSettingsElevated();
    }

    private void LoadRecipients()
    {
        _allRecipients.Clear();
        _recipientList.Items.Clear();

        var clients = _registry.GetOnlineClients();

        // Unique users
        var users = clients
            .GroupBy(c => $@"{c.Domain}\{c.Username}")
            .Select(g => g.First())
            .ToList();

        foreach (var c in users)
        {
            _allRecipients.Add(new RecipientItem(
                $@"{c.Domain}\{c.Username} — {c.ComputerName} — {c.SessionType}",
                "User",
                $@"{c.Domain}\{c.Username}"));
        }

        // Unique computers
        var computers = clients
            .GroupBy(c => c.ComputerName)
            .Select(g => g.First())
            .ToList();

        foreach (var c in computers)
        {
            _allRecipients.Add(new RecipientItem(
                $"[PC] {c.ComputerName}",
                "Computer",
                c.ComputerName));
        }

        // All
        if (_settings.Settings.AllowSendToAll)
        {
            _allRecipients.Add(new RecipientItem("*** TUTTI ***", "All", "all"));
        }

        FilterRecipients();
        _statusLabel.Text = $"{clients.Count} client online.";
        _statusLabel.ForeColor = Color.DarkGray;
    }

    private void FilterRecipients()
    {
        var query = _searchBox.Text.Trim().ToLower();
        _recipientList.Items.Clear();

        foreach (var r in _allRecipients)
        {
            if (string.IsNullOrEmpty(query) || r.DisplayName.ToLower().Contains(query))
                _recipientList.Items.Add(r);
        }
    }

    private void SendButton_Click(object? sender, EventArgs e)
    {
        var selected = _recipientList.CheckedItems.Cast<RecipientItem>().ToList();
        if (selected.Count == 0)
        {
            ShowStatus("Seleziona almeno un destinatario.", Color.OrangeRed);
            return;
        }

        var body = _bodyBox.Text.Trim();
        if (string.IsNullOrEmpty(body))
        {
            ShowStatus("Il messaggio non può essere vuoto.", Color.OrangeRed);
            return;
        }

        try
        {
            foreach (var r in selected)
                _sender.Send(r.TargetType, r.Target, "MESSAGGIO IMPORTANTE", body);

            ShowStatus($"Messaggio inviato a {selected.Count} destinatario/i.", Color.Green);
            _bodyBox.Clear();

            foreach (int i in _recipientList.CheckedIndices.Cast<int>().ToList())
                _recipientList.SetItemChecked(i, false);
        }
        catch (Exception ex)
        {
            ShowStatus($"Errore: {ex.Message}", Color.OrangeRed);
        }
    }

    private void ShowStatus(string text, Color color)
    {
        _statusLabel.Text = text;
        _statusLabel.ForeColor = color;
    }

    private record RecipientItem(string DisplayName, string TargetType, string Target)
    {
        public override string ToString() => DisplayName;
    }
}
