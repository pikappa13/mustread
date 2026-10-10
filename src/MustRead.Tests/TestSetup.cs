using System.Runtime.CompilerServices;
using MustRead.App.Utils;

namespace MustRead.Tests;

internal static class TestSetup
{
    // Keep test runs from writing into the real %LOCALAPPDATA%\MustRead\logs.
    [ModuleInitializer]
    internal static void Init()
        => Log.Directory = Path.Combine(Path.GetTempPath(), "MustReadTestLogs");
}
