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

    // Machine-wide values for managed deployments (registry, HKLM):
    //  - SOFTWARE\MustRead           written by the MSI (SERVERPATH=...), used only
    //                                when settings.json has no server path;
    //  - SOFTWARE\Policies\MustRead  set by Group Policy, always wins.
    private const string DefaultsKey = @"SOFTWARE\MustRead";
    private const string PolicyKey = @"SOFTWARE\Policies\MustRead";

    public bool ServerPathEnforcedByPolicy { get; private set; }

    public void Load()
    {
        Settings = LoadFromFile();

        ServerPathEnforcedByPolicy = false;
        var policyPath = ReadMachineValue(PolicyKey, "ServerPath");
        if (!string.IsNullOrWhiteSpace(policyPath))
        {
            Settings.ServerPath = policyPath.Trim();
            ServerPathEnforcedByPolicy = true;
        }
        else if (string.IsNullOrWhiteSpace(Settings.ServerPath))
        {
            var defaultPath = ReadMachineValue(DefaultsKey, "ServerPath");
            if (!string.IsNullOrWhiteSpace(defaultPath))
                Settings.ServerPath = defaultPath.Trim();
        }
    }

    private AppSettings LoadFromFile()
    {
        if (!File.Exists(LocalSettingsPath))
            return new AppSettings();

        try
        {
            var json = File.ReadAllText(LocalSettingsPath);
            return JsonHelper.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            Log.Warn($"Impostazioni illeggibili in '{LocalSettingsPath}', uso i valori predefiniti", ex);
            return new AppSettings();
        }
    }

    protected virtual string? ReadMachineValue(string keyPath, string name)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyPath);
            return key?.GetValue(name) as string;
        }
        catch (Exception ex)
        {
            Log.Warn($"Lettura registro HKLM\\{keyPath} fallita", ex);
            return null;
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
