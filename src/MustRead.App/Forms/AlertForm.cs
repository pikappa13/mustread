using MustRead.App.Models;
using MustRead.App.Services;

namespace MustRead.App.Forms;

public class AlertForm : Form
{
    private readonly AlertMessage _message;
    private readonly MessageReceiverService _receiver;

    private Label _titleLabel = null!;
    private Label _bodyLabel = null!;
    private Label _senderLabel = null!;
    private Button _ackButton = null!;
    private bool _acknowledged = false;

    public AlertForm(AlertMessage message, MessageReceiverService receiver)
    {
        _message = message;
        _receiver = receiver;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        FormBorderStyle = FormBorderStyle.None;
        WindowState = FormWindowState.Maximized;
        TopMost = true;
        ShowInTaskbar = true;
        BackColor = Color.FromArgb(30, 30, 30);
        KeyPreview = true;
        Utils.UiHelper.ApplyIcon(this);

        // Intercept keyboard close attempts
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape ||
                e.KeyCode == Keys.F4 ||
                (e.Alt && e.KeyCode == Keys.F4))
                e.Handled = true;
        };

        _titleLabel = new Label
        {
            Text = _message.Title,
            Font = new Font("Segoe UI", 28f, FontStyle.Bold),
            ForeColor = Color.FromArgb(255, 80, 80),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.None
        };

        _bodyLabel = new Label
        {
            Text = _message.Body,
            Font = new Font("Segoe UI", 18f),
            ForeColor = Color.White,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.None
        };

        _senderLabel = new Label
        {
            Text = $"Da: {_message.SenderDomain}\\{_message.SenderUsername}  —  {_message.CreatedAt:dd/MM/yyyy HH:mm}",
            Font = new Font("Segoe UI", 11f),
            ForeColor = Color.LightGray,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.None
        };

        _ackButton = new Button
        {
            Text = "HO LETTO",
            Font = new Font("Segoe UI", 16f, FontStyle.Bold),
            BackColor = Color.FromArgb(0, 122, 204),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Size = new Size(220, 60)
        };
        _ackButton.FlatAppearance.BorderSize = 0;
        _ackButton.Click += AckButton_Click;

        var footerLabel = new Label
        {
            Text = "sviluppato da Patrick Battistini",
            Font = new Font("Segoe UI", 7.5f),
            ForeColor = Color.FromArgb(80, 80, 80),
            AutoSize = true,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };

        Controls.AddRange(new Control[] { _titleLabel, _bodyLabel, _senderLabel, _ackButton, footerLabel });

        Load += (_, _) =>
        {
            LayoutControls();
            footerLabel.Location = new Point(ClientSize.Width - footerLabel.Width - 6, ClientSize.Height - footerLabel.Height - 4);
        };
        Resize += (_, _) =>
        {
            LayoutControls();
            footerLabel.Location = new Point(ClientSize.Width - footerLabel.Width - 6, ClientSize.Height - footerLabel.Height - 4);
        };

        ResumeLayout(false);
    }

    private void LayoutControls()
    {
        var w = ClientSize.Width;
        var h = ClientSize.Height;
        var pad = 60;

        _titleLabel.SetBounds(pad, (int)(h * 0.25), w - pad * 2, 60);
        _bodyLabel.SetBounds(pad, (int)(h * 0.38), w - pad * 2, (int)(h * 0.28));
        _senderLabel.SetBounds(pad, (int)(h * 0.70), w - pad * 2, 30);
        _ackButton.Location = new Point((w - _ackButton.Width) / 2, (int)(h * 0.78));
    }

    private void AckButton_Click(object? sender, EventArgs e)
    {
        // Guard against double-clicks / re-entrancy: once acknowledged we are
        // closing, ignore any further clicks.
        if (_acknowledged) return;
        _acknowledged = true;

        // Mark read first (updates the in-memory guard so the poll timer won't
        // re-open this alert), then close. MarkAsRead swallows its own IO errors,
        // so a slow/locked SMB share can never block the window from closing.
        _ackButton.Enabled = false;
        _receiver.MarkAsRead(_message.MessageId);
        Close();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Block all close attempts until the user clicks HO LETTO
        if (!_acknowledged)
            e.Cancel = true;

        base.OnFormClosing(e);
    }

    // Prevent Alt+F4 at message-loop level
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Alt | Keys.F4))
            return true;
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
