using Microsoft.Win32;

namespace MustRead.App.Services;

public static class AutostartService
{
    // Settable so tests never touch the real Run key of the machine running them.
    public static string RegistryKey { get; set; } = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "MustRead";

    /// <summary>Passed on automatic starts: a second instance then exits silently.</summary>
    public const string AutostartArgument = "--autostart";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, false);
            return key?.GetValue(ValueName) != null;
        }
        catch { return false; }
    }

    public static void Enable(string exePath)
    {
        // CreateSubKey opens the key, or creates it when a fresh profile lacks it.
        using var key = Registry.CurrentUser.CreateSubKey(RegistryKey, true)
            ?? throw new InvalidOperationException("Impossibile aprire il registro di sistema.");
        key.SetValue(ValueName, $"\"{exePath}\" {AutostartArgument}");
    }

    public static void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
