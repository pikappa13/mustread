using MustRead.App.Services;
using Xunit;

namespace MustRead.Tests.Services;

public class ConnectionStatusTests
{
    [Fact]
    public void IsOnline_IsUnknownUntilFirstReport()
        => Assert.Null(new ConnectionStatus().IsOnline);

    [Fact]
    public void ReportFailure_ReturnsTrueOnlyAtStartOfOutage()
    {
        var status = new ConnectionStatus();
        Assert.True(status.ReportFailure("op"));
        Assert.False(status.ReportFailure("op"));
        Assert.False(status.IsOnline);
    }

    [Fact]
    public void ReportSuccess_ReturnsTrueOnlyWhenRecovering()
    {
        var status = new ConnectionStatus();
        Assert.False(status.ReportSuccess());
        status.ReportFailure("op", new IOException("down"));
        Thread.Sleep(15);
        Assert.True(status.ReportSuccess());
        Assert.False(status.ReportSuccess());
        Assert.True(status.IsOnline);
        Assert.Contains("down", status.LastError);
    }
}

public class DiagnosticsServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly TestableSettingsService _settings;
    private readonly DiagnosticsService _diagnostics;

    public DiagnosticsServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"MustReadDiag_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _settings = new TestableSettingsService(_testDir);
        _settings.Settings.ServerPath = _testDir;

        var session = new SessionInfoService();
        var status = new ConnectionStatus();
        var history = new HistoryService(_settings, status);
        var sender = new MessageSenderService(_settings, session, history);
        _diagnostics = new DiagnosticsService(_settings, session, history, sender, status);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    [Fact]
    public void Run_WithoutServerPath_ReportsConfigurationError()
    {
        _settings.Settings.ServerPath = "";
        var results = _diagnostics.Run();
        Assert.Contains(results, r => r.Name == "Configurazione" && r.Level == DiagnosticLevel.Error);
    }

    [Fact]
    public void Run_WithUnreachableShare_ReportsError()
    {
        _settings.Settings.ServerPath = Path.Combine(_testDir, "does-not-exist");
        var results = _diagnostics.Run();
        Assert.Contains(results, r => r.Name == "Cartella condivisa" && r.Level == DiagnosticLevel.Error);
    }

    [Fact]
    public void Run_WithHealthyShare_HasNoErrorsAndLeavesNoProbeFiles()
    {
        var results = _diagnostics.Run();

        Assert.DoesNotContain(results, r => r.Level == DiagnosticLevel.Error);
        Assert.Contains(results, r => r.Name.StartsWith("Scrittura heartbeat") && r.Level == DiagnosticLevel.Ok);
        Assert.Empty(Directory.GetFiles(_testDir, ".mustread-probe-*", SearchOption.AllDirectories));
    }

    [Fact]
    public void Run_WithMalformedSenderList_Warns()
    {
        Directory.CreateDirectory(Path.Combine(_testDir, "config"));
        File.WriteAllText(Path.Combine(_testDir, "config", "authorized_senders.json"), "{ broken");

        var results = _diagnostics.Run();
        Assert.Contains(results, r => r.Name == "Autorizzazione invio" && r.Level == DiagnosticLevel.Warning);
    }

    [Fact]
    public void Format_ContainsEveryCheck()
    {
        var results = _diagnostics.Run();
        var text = DiagnosticsService.Format(results);
        foreach (var r in results)
            Assert.Contains(r.Name, text);
    }
}
