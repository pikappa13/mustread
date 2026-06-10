namespace MustRead.App.Utils;

/// <summary>
/// Append-safe writes to shared JSONL files across multiple machines.
/// Uses a .lock sidecar file with retry logic to serialise concurrent writes.
/// </summary>
public static class FileLockHelper
{
    private const int MaxRetries = 20;
    private const int RetryDelayMs = 100;

    public static void AppendLine(string filePath, string line)
    {
        var lockPath = filePath + ".lock";
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                using var lockStream = new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None);

                File.AppendAllText(filePath, line + Environment.NewLine);
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(RetryDelayMs);
            }
        }

        // Last attempt without lock — better to write than to silently drop
        File.AppendAllText(filePath, line + Environment.NewLine);
    }

    public static void WriteAllTextSafe(string filePath, string content)
    {
        var lockPath = filePath + ".lock";
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                using var lockStream = new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None);

                File.WriteAllText(filePath, content);
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(RetryDelayMs);
            }
        }

        File.WriteAllText(filePath, content);
    }
}
