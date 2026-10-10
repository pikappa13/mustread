namespace MustRead.App.Utils;

/// <summary>
/// Safe writes to shared files across multiple processes and machines.
/// Uses a .lock sidecar file with retry + jittered backoff to serialise writers.
/// Never writes without holding the lock: if the lock cannot be acquired in time
/// a <see cref="TimeoutException"/> is thrown so the caller can log or retry.
/// Whole-file writes go through a temp file + rename so readers never see a
/// partially written file.
/// </summary>
public static class FileLockHelper
{
    private const int MaxRetries = 20;
    private const int BaseDelayMs = 50;
    private const int MaxDelayMs = 250; // ~4.5s worst case before TimeoutException

    public static void AppendLine(string filePath, string line)
    {
        WithLock(filePath, () =>
        {
            using var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
            using var writer = new StreamWriter(stream);
            writer.Write(line + Environment.NewLine);
            writer.Flush();
            stream.Flush(flushToDisk: true);
        });
    }

    public static void WriteAllTextSafe(string filePath, string content)
    {
        WithLock(filePath, () => WriteAtomic(filePath, content));
    }

    /// <summary>
    /// Runs <paramref name="action"/> while holding the sidecar lock of
    /// <paramref name="filePath"/>. Use for read-modify-write sequences.
    /// </summary>
    public static void WithLock(string filePath, Action action)
    {
        var lockPath = filePath + ".lock";
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            FileStream? lockStream = null;
            try
            {
                lockStream = new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None);
            }
            catch (IOException)
            {
                var delay = Math.Min(MaxDelayMs, BaseDelayMs * (attempt + 1));
                Thread.Sleep(delay + Random.Shared.Next(0, BaseDelayMs));
                continue;
            }

            using (lockStream)
            {
                action();
                return;
            }
        }

        throw new TimeoutException($"Impossibile ottenere il lock su '{filePath}'.");
    }

    /// <summary>
    /// Reads a file that other processes may be appending to at the same time
    /// (history JSONL). File.ReadAllText would fail with "file in use".
    /// </summary>
    public static string ReadAllTextShared(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static string[] ReadAllLinesShared(string filePath)
        => ReadAllTextShared(filePath).Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// Writes to a temp file in the same directory, flushes it to disk, then
    /// renames it over the destination. Caller is responsible for locking.
    /// </summary>
    public static void WriteAtomic(string filePath, string content)
    {
        var tempPath = $"{filePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(content);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }
            File.Move(tempPath, filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* best effort */ }
            }
        }
    }
}
