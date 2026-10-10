using System.Diagnostics;
using MustRead.App.Utils;
using Xunit;

namespace MustRead.Tests;

/// <summary>
/// Runs real, separate processes (MustRead.TestHost) against the same files, the
/// way several PCs or RDP sessions hit the share and the per-user state.
/// In-process thread tests cannot catch cross-process lost updates.
/// </summary>
public class MultiProcessTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _shareDir;

    public MultiProcessTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"MustReadMulti_{Guid.NewGuid():N}");
        _shareDir = Path.Combine(_testDir, "share");
        Directory.CreateDirectory(_shareDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            try { Directory.Delete(_testDir, recursive: true); }
            catch { /* best effort */ }
        }
    }

    private static Process Start(params string[] args)
    {
        var exe = Path.Combine(AppContext.BaseDirectory, "MustRead.TestHost.exe");
        var psi = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true
        };
        foreach (var a in args)
            psi.ArgumentList.Add(a);
        return Process.Start(psi)!;
    }

    private static void WaitAll(IEnumerable<Process> processes, bool expectSuccess = true)
    {
        foreach (var p in processes)
        {
            var stderr = p.StandardError.ReadToEnd();
            Assert.True(p.WaitForExit(60_000), "processo bloccato");
            if (expectSuccess)
                Assert.True(p.ExitCode == 0, $"exit {p.ExitCode}: {stderr}");
            p.Dispose();
        }
    }

    [Fact]
    public void AppendLine_FromFourProcesses_LosesAndCorruptsNothing()
    {
        var file = Path.Combine(_shareDir, "history", "read.jsonl");
        var procs = Enumerable.Range(0, 4).Select(i => Start("append", file, "50", $"p{i}")).ToList();
        WaitAll(procs);

        var lines = File.ReadAllLines(file);
        Assert.Equal(200, lines.Length);
        Assert.All(lines, l => Assert.Matches("^\\{\"tag\":\"p\\d\",\"i\":\\d+\\}$", l));
        Assert.Equal(200, lines.Distinct().Count());
    }

    [Fact]
    public void MarkAsRead_FromFourProcessesOfSameUser_KeepsEveryAck()
    {
        // Four sessions of the same user share one per-user read_messages.json.
        var baseDir = Path.Combine(_testDir, "user");
        var procs = Enumerable.Range(0, 4).Select(i => Start("markread", baseDir, _shareDir, $"s{i}", "25")).ToList();
        WaitAll(procs);

        var ids = JsonHelper.Deserialize<List<string>>(File.ReadAllText(Path.Combine(baseDir, "read_messages.json")))!;
        for (var s = 0; s < 4; s++)
            for (var i = 0; i < 25; i++)
                Assert.Contains($"s{s}-{i}", ids);
    }

    [Fact]
    public void Ack_SurvivesProcessCrashRightAfterClick()
    {
        var baseDir = Path.Combine(_testDir, "user");
        // The share is unreachable, so the read record can only be in the local outbox.
        var blocked = Path.Combine(_testDir, "blocked");
        File.WriteAllText(blocked, "x");

        var p = Start("crash-after-ack", baseDir, blocked, "crash-id");
        WaitAll(new[] { p }, expectSuccess: false);

        Assert.Contains("crash-id", File.ReadAllText(Path.Combine(baseDir, "read_messages.json")));
        Assert.Contains(Directory.GetFiles(Path.Combine(baseDir, "outbox"), "*.json"),
            f => File.ReadAllText(f).Contains("crash-id"));
    }

    [Fact]
    public void ReadRecords_FromProcessesOfDifferentUsers_AllReachTheShare()
    {
        var procs = Enumerable.Range(0, 4)
            .Select(i => Start("record", Path.Combine(_testDir, $"user{i}"), _shareDir, $"u{i}", "20"))
            .ToList();
        WaitAll(procs);

        var lines = File.ReadAllLines(Path.Combine(_shareDir, "history", "read.jsonl"));
        Assert.Equal(80, lines.Length);
        for (var u = 0; u < 4; u++)
            Assert.Equal(20, lines.Count(l => l.Contains($"\"u{u}-")));
    }
}
