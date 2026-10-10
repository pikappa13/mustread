namespace MustRead.App.Utils;

public static class UiHelper
{
    private static Icon? _cachedIcon;

    public static Icon LoadAppIcon()
    {
        if (_cachedIcon != null) return _cachedIcon;
        try
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("MustRead.App.Resources.app.ico");
            if (stream != null)
            {
                _cachedIcon = new Icon(stream);
                return _cachedIcon;
            }
        }
        catch (Exception ex)
        {
            Log.Warn("Icona applicazione non caricata", ex);
        }
        return SystemIcons.Information;
    }

    public static void ApplyIcon(Form form) => form.Icon = LoadAppIcon();

    public static void AddFooter(Form form)
    {
        var label = new Label
        {
            Text = "sviluppato da Patrick Battistini",
            Font = new Font("Segoe UI", 7.5f, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(160, 160, 160),
            AutoSize = true,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            Cursor = Cursors.Default
        };

        form.Controls.Add(label);
        label.BringToFront();

        // Position after layout is known
        void reposition(object? s, EventArgs e)
        {
            label.Location = new Point(
                form.ClientSize.Width - label.Width - 6,
                form.ClientSize.Height - label.Height - 4);
        }

        form.Resize += reposition;
        form.Load += reposition;
    }
}
