using MustRead.App.Models;
using MustRead.App.Utils;

namespace MustRead.App.Services;

public class SettingsService
{
    protected virtual string LocalSettingsPath => @"C:\ProgramData\MustRead\settings.json";

    // Read-state is per Windows user: a machine-wide file mixed acknowledgements
    // of different users on shared PCs and RDP servers.
    protected virtual string LocalReadMessagesPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MustRead", "read_messages.json");

    // v1.0.x machine-wide read-state, imported once to avoid re-showing old messages.
    protected virtual string LegacyReadMessagesPath => @"C:\ProgramData\MustRead\read_messages.json";

    // Per-user queue of history records not yet written to the share.
    protected virtual string LocalOutboxPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MustRead", "outbox");

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
        catch (Exception ex)
        {
            Log.Warn($"Impostazioni illeggibili in '{LocalSettingsPath}', uso i valori predefiniti", ex);
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
        => Path.Combine(Settings.ServerPath, "inbox", "users",
            $"{PathSafety.EnsureSafeSegment(domain)}_{PathSafety.EnsureSafeSegment(username)}");

    public string GetInboxComputerPath(string computerName)
        => Path.Combine(Settings.ServerPath, "inbox", "computers", PathSafety.EnsureSafeSegment(computerName));

    public string GetInboxAllPath()
        => Path.Combine(Settings.ServerPath, "inbox", "all");

    public string GetClientsPath()
        => Path.Combine(Settings.ServerPath, "clients");

    public string GetHistoryPath(string file)
        => Path.Combine(Settings.ServerPath, "history", file);

    public string GetConfigPath(string file)
        => Path.Combine(Settings.ServerPath, "config", file);

    public string GetLocalReadMessagesPath() => LocalReadMessagesPath;

    public string GetLegacyReadMessagesPath() => LegacyReadMessagesPath;

    public string GetLocalOutboxPath() => LocalOutboxPath;
}
