namespace MustRead.App.Utils;

/// <summary>
/// Minimal local file logger. Writes one file per day under
/// %LOCALAPPDATA%\MustRead\logs and keeps the last <see cref="RetentionDays"/> days.
/// Never throws: logging must not be able to break the app.
/// Message bodies must never be logged — only ids, paths and error details.
/// </summary>
public static class Log
{
    private const int RetentionDays = 14;
    private static readonly object Sync = new();
    private static DateTime _lastCleanup = DateTime.MinValue;

    public static string Directory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MustRead", "logs");

    public static void Info(string message) => Write("INFO", message, null);

    public static void Warn(string message, Exception? ex = null) => Write("WARN", message, ex);

    public static void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    private static void Write(string level, string message, Exception? ex)
    {
        try
        {
            var now = DateTime.Now;
            var line = $"{now:yyyy-MM-dd HH:mm:ss.fff} [{level}] [pid {Environment.ProcessId}] {message}";
            if (ex != null)
                line += $" | {ex.GetType().Name}: {ex.Message}";

            lock (Sync)
            {
                System.IO.Directory.CreateDirectory(Directory);
                var path = Path.Combine(Directory, $"mustread-{now:yyyyMMdd}.log");
                File.AppendAllText(path, line + Environment.NewLine);

                if (now - _lastCleanup > TimeSpan.FromHours(6))
                {
                    _lastCleanup = now;
                    CleanupOldFiles(now);
                }
            }
        }
        catch
        {
            // Logging is best-effort by design.
        }
    }

    private static void CleanupOldFiles(DateTime now)
    {
        foreach (var file in System.IO.Directory.GetFiles(Directory, "mustread-*.log"))
        {
            if (File.GetLastWriteTime(file) < now.AddDays(-RetentionDays))
                File.Delete(file);
        }
    }
}
