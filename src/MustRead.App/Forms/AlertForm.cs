using System.Runtime.InteropServices;
using MustRead.App.Models;
using MustRead.App.Services;

namespace MustRead.App.Forms;

public class AlertForm : Form
{
    private readonly AlertMessage _message;
    private readonly MessageReceiverService _receiver;

    private Label _titleLabel = null!;
    private Label _bodyLabel = null!;
    private Panel _bodyPanel = null!;
    private Label _senderLabel = null!;
    private Button _ackButton = null!;
    private bool _acknowledged = false;
    // Dark covers on the other monitors, so the alert cannot be ignored by
    // working on a second screen.
    private readonly List<Form> _screenCovers = new();
    private System.Windows.Forms.Timer _keepOnTopTimer = null!;

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
        // Maximize on the primary monitor; the others get a cover (see Load).
        StartPosition = FormStartPosition.Manual;
        Bounds = PrimaryScreen.Bounds;
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

        // UseMnemonic = false on every label showing message text: otherwise
        // WinForms treats "&" as an accelerator prefix and "R&D" shows as "RD".
        _titleLabel = new Label
        {
            Text = _message.Title,
            Font = new Font("Segoe UI", 28f, FontStyle.Bold),
            ForeColor = Color.FromArgb(255, 80, 80),
            AutoSize = false,
            AutoEllipsis = true,
            UseMnemonic = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.None
        };

        // The body is sized to its text (see LayoutBody) inside a scrolling panel,
        // so long messages are never cut off.
        _bodyLabel = new Label
        {
            Text = _message.Body,
            Font = new Font("Segoe UI", 18f),
            ForeColor = Color.White,
            AutoSize = false,
            UseMnemonic = false,
            TextAlign = ContentAlignment.TopCenter
        };
        _bodyPanel = new Panel
        {
            AutoScroll = true,
            BackColor = BackColor
        };
        _bodyPanel.Controls.Add(_bodyLabel);

        _senderLabel = new Label
        {
            Text = $"Da: {_message.SenderDomain}\\{_message.SenderUsername}  —  {_message.CreatedAt:dd/MM/yyyy HH:mm}",
            Font = new Font("Segoe UI", 11f),
            ForeColor = Color.LightGray,
            AutoSize = false,
            UseMnemonic = false,
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

        Controls.AddRange(new Control[] { _titleLabel, _bodyPanel, _senderLabel, _ackButton, footerLabel });

        Load += (_, _) =>
        {
            LayoutControls();
            footerLabel.Location = new Point(ClientSize.Width - footerLabel.Width - 6, ClientSize.Height - footerLabel.Height - 4);
            ShowScreenCovers();
            _keepOnTopTimer.Start();
        };
        FormClosed += (_, _) =>
        {
            _keepOnTopTimer.Dispose();
            foreach (var cover in _screenCovers)
                cover.Dispose();
            _screenCovers.Clear();
        };

        // Other topmost windows can still cover the alert: re-assert it periodically.
        _keepOnTopTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        _keepOnTopTimer.Tick += (_, _) => KeepOnTop();
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

        var contentWidth = Math.Max(100, w - pad * 2);

        // Title: as many lines as it needs, up to about a fifth of the screen.
        var titleSize = TextRenderer.MeasureText(_titleLabel.Text, _titleLabel.Font,
            new Size(contentWidth, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        var titleHeight = Math.Clamp(titleSize.Height + 10, 60, Math.Max(60, (int)(h * 0.2)));
        var titleTop = (int)(h * 0.12);
        _titleLabel.SetBounds(pad, titleTop, contentWidth, titleHeight);

        var bodyTop = titleTop + titleHeight + 20;
        var bodyBottom = (int)(h * 0.68);
        _bodyPanel.SetBounds(pad, bodyTop, contentWidth, Math.Max(80, bodyBottom - bodyTop));
        LayoutBody();

        _senderLabel.SetBounds(pad, (int)(h * 0.70), contentWidth, 30);
        _ackButton.Location = new Point((w - _ackButton.Width) / 2, (int)(h * 0.78));
    }

    private void LayoutBody()
    {
        // Wrap the text to the panel width (leaving room for the scrollbar),
        // then centre it when it fits, or start at the top so it can scroll.
        var available = Math.Max(50, _bodyPanel.Width - SystemInformation.VerticalScrollBarWidth);
        var textSize = TextRenderer.MeasureText(_bodyLabel.Text, _bodyLabel.Font,
            new Size(available, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);

        _bodyPanel.AutoScrollPosition = Point.Empty;
        _bodyLabel.Size = new Size(available, textSize.Height + 10);
        var y = Math.Max(0, (_bodyPanel.Height - _bodyLabel.Height) / 2);
        _bodyLabel.Location = new Point(0, y);
    }

    private static Screen PrimaryScreen => Screen.PrimaryScreen ?? Screen.AllScreens[0];

    private void ShowScreenCovers()
    {
        foreach (var screen in Screen.AllScreens)
        {
            if (screen.Bounds == PrimaryScreen.Bounds) continue;

            var cover = new ScreenCoverForm(screen.Bounds, BackColor, this);
            _screenCovers.Add(cover);
            cover.Show(this);
        }
    }

    private void KeepOnTop()
    {
        if (_acknowledged || !IsHandleCreated) return;

        foreach (var cover in _screenCovers)
            if (cover.IsHandleCreated)
                SetWindowPos(cover.Handle, HwndTopmost, 0, 0, 0, 0, TopmostFlags);
        // Back on top without taking the keyboard focus: a user typing elsewhere
        // must not confirm the alert by accident with Enter or Space.
        SetWindowPos(Handle, HwndTopmost, 0, 0, 0, 0, TopmostFlags);
    }

    private static readonly IntPtr HwndTopmost = new(-1);
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private const uint TopmostFlags = SwpNoSize | SwpNoMove | SwpNoActivate | SwpShowWindow;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _keepOnTopTimer?.Dispose();
        base.Dispose(disposing);
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

/// <summary>
/// Dark, borderless cover for a secondary monitor while an alert is open.
/// Cannot be closed by the user; clicking it brings the alert back to front.
/// </summary>
internal class ScreenCoverForm : Form
{
    private readonly Form _alert;

    public ScreenCoverForm(Rectangle bounds, Color backColor, Form alert)
    {
        _alert = alert;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = bounds;
        TopMost = true;
        ShowInTaskbar = false;
        BackColor = backColor;

        var label = new Label
        {
            Text = "Messaggio importante sullo schermo principale.\nLeggilo e conferma con \"HO LETTO\".",
            Font = new Font("Segoe UI", 18f),
            ForeColor = Color.LightGray,
            AutoSize = false,
            UseMnemonic = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill
        };
        label.Click += (_, _) => _alert.Activate();
        Click += (_, _) => _alert.Activate();
        Controls.Add(label);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Closed only together with the alert (by disposal), never by the user.
        if (e.CloseReason == CloseReason.UserClosing)
            e.Cancel = true;
        base.OnFormClosing(e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Alt | Keys.F4))
            return true;
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
