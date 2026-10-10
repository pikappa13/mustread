using System.Diagnostics;
using MustRead.App.Utils;

namespace MustRead.App.Forms;

/// <summary>
/// Shows the "Verifica connessione" report with buttons to copy it and to open
/// the local log folder.
/// </summary>
public class DiagnosticsForm : Form
{
    public DiagnosticsForm(string appName, string report)
    {
        Text = $"{appName} — Verifica connessione";
        Size = new Size(720, 480);
        MinimumSize = new Size(480, 320);
        StartPosition = FormStartPosition.CenterScreen;
        UiHelper.ApplyIcon(this);

        var box = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Font = new Font("Consolas", 10f),
            Text = report,
            AccessibleName = "Risultato della verifica"
        };

        var copyButton = new Button { Text = "&Copia", AutoSize = true };
        copyButton.Click += (_, _) => Clipboard.SetText(report);

        var logsButton = new Button { Text = "Apri cartella &log", AutoSize = true };
        logsButton.Click += (_, _) =>
        {
            try
            {
                Directory.CreateDirectory(Log.Directory);
                Process.Start(new ProcessStartInfo { FileName = Log.Directory, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Log.Warn("Apertura cartella log fallita", ex);
            }
        };

        var closeButton = new Button { Text = "Chiudi", AutoSize = true, DialogResult = DialogResult.Cancel };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Padding = new Padding(6)
        };
        buttons.Controls.AddRange(new Control[] { closeButton, logsButton, copyButton });

        Controls.Add(box);
        Controls.Add(buttons);
        CancelButton = closeButton;
    }
}
