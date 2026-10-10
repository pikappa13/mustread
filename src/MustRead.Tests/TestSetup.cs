using System.Runtime.CompilerServices;
using MustRead.App.Utils;

namespace MustRead.Tests;

internal static class TestSetup
{
    // Keep test runs from writing into the real %LOCALAPPDATA%\MustRead\logs.
    [ModuleInitializer]
    internal static void Init()
    {
        Log.Directory = Path.Combine(Path.GetTempPath(), "MustReadTestLogs");
        // Keep autostart tests away from the real HKCU Run key (they would
        // delete a developer's actual MustRead autostart entry).
        MustRead.App.Services.AutostartService.RegistryKey = $@"SOFTWARE\MustReadTests\{Guid.NewGuid():N}\Run";
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try { Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(@"SOFTWARE\MustReadTests", false); }
            catch { /* best effort */ }
        };
    }
}
