using MustRead.App.Models;
using MustRead.App.Utils;

namespace MustRead.App.Services;

public class SettingsService
{
    protected virtual string LocalSettingsPath => @"C:\ProgramData\MustRead\settings.json";
    protected virtual string LocalReadMessagesPath => @"C:\ProgramData\MustRead\read_messages.json";

    public AppSettings Settings { get; private set; } = new();

    public void Load()
    {
        if (!File.Exists(LocalSettingsPath))
        {
            Settings = new AppSettings();
            return;
        }

        try
        {
            var json = File.ReadAllText(LocalSettingsPath);
            Settings = JsonHelper.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            Settings = new AppSettings();
        }
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(LocalSettingsPath)!;
        Directory.CreateDirectory(dir);
        FileLockHelper.WriteAllTextSafe(LocalSettingsPath, JsonHelper.Serialize(Settings));
    }

    public string GetInboxUserPath(string domain, string username)
        => Path.Combine(Settings.ServerPath, "inbox", "users", $"{domain}_{username}");

    public string GetInboxComputerPath(string computerName)
        => Path.Combine(Settings.ServerPath, "inbox", "computers", computerName);

    public string GetInboxAllPath()
        => Path.Combine(Settings.ServerPath, "inbox", "all");

    public string GetClientsPath()
        => Path.Combine(Settings.ServerPath, "clients");

    public string GetHistoryPath(string file)
        => Path.Combine(Settings.ServerPath, "history", file);

    public string GetConfigPath(string file)
        => Path.Combine(Settings.ServerPath, "config", file);

    public string GetLocalReadMessagesPath() => LocalReadMessagesPath;
}
