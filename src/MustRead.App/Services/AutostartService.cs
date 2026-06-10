using Microsoft.Win32;

namespace MustRead.App.Services;

public static class AutostartService
{
    private const string RegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "MustRead";

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
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, true)
            ?? throw new InvalidOperationException("Impossibile aprire il registro di sistema.");
        key.SetValue(ValueName, $"\"{exePath}\"");
    }

    public static void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
