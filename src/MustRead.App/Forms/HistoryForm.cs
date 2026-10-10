using MustRead.App.Services;
using MustRead.App.Utils;

namespace MustRead.App.Forms;

public class HistoryForm : Form
{
    private readonly SettingsService _settings;
    private TabControl _tabs = null!;

    public HistoryForm(SettingsService settings)
    {
        _settings = settings;
        InitializeComponent();
        LoadAll();
    }

    private void InitializeComponent()
    {
        Text = "Storico messaggi";
        Size = new Size(800, 500);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10f);

        _tabs = new TabControl { Dock = DockStyle.Fill };

        var btnRefresh = new Button
        {
            Text = "Aggiorna",
            Dock = DockStyle.Bottom,
            Height = 34
        };
        btnRefresh.Click += (_, _) => LoadAll();

        Controls.Add(_tabs);
        Controls.Add(btnRefresh);
        Utils.UiHelper.ApplyIcon(this);
        Utils.UiHelper.AddFooter(this);
    }

    private void LoadAll()
    {
        _tabs.TabPages.Clear();
        _tabs.TabPages.Add(BuildTab("Inviati", "sent.jsonl"));
        _tabs.TabPages.Add(BuildTab("Mostrati", "shown.jsonl"));
        _tabs.TabPages.Add(BuildTab("Letti", "read.jsonl"));
    }

    private TabPage BuildTab(string title, string file)
    {
        var page = new TabPage(title);
        var box = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            Font = new Font("Consolas", 9f),
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.LightGreen,
            ScrollBars = RichTextBoxScrollBars.Both,
            WordWrap = false
        };

        try
        {
            var path = _settings.GetHistoryPath(file);
            if (File.Exists(path))
            {
                var lines = Utils.FileLockHelper.ReadAllLinesShared(path);
                box.Text = string.Join(Environment.NewLine, lines.Reverse());
            }
            else
            {
                box.Text = "(nessun record)";
            }
        }
        catch (Exception ex)
        {
            Utils.Log.Warn("Lettura storico fallita", ex);
            box.Text = $"Errore lettura: {ex.Message}";
        }

        page.Controls.Add(box);
        return page;
    }
}
