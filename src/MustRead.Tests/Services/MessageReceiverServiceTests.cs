using MustRead.App.Models;
using MustRead.App.Services;
using MustRead.App.Utils;
using Xunit;

namespace MustRead.Tests.Services;

public class MessageReceiverServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly TestableSettingsService _settings;
    private readonly SessionInfoService _session;
    private readonly HistoryService _history;
    private readonly MessageReceiverService _receiver;

    public MessageReceiverServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"MustReadReceiver_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);

        _settings = new TestableSettingsService(_testDir);
        _settings.Settings.ServerPath = _testDir;
        _settings.Settings.PollingSeconds = 1;

        _session = new SessionInfoService();
        _history = new HistoryService(_settings);
        _receiver = new MessageReceiverService(_settings, _session, _history);
    }

    public void Dispose()
    {
        _receiver.Stop();
        if (Directory.Exists(_testDir))
        {
            try { Directory.Delete(_testDir, recursive: true); }
            catch { /* background history writers may still hold a handle briefly */ }
        }
    }

    private AlertMessage WriteMessageToInbox(string folder, string body = "test body", string? id = null)
    {
        Directory.CreateDirectory(folder);
        var msg = new AlertMessage
        {
            MessageId = id ?? AlertMessage.GenerateId(),
            Body = body,
            Title = "TEST",
            TargetType = "All",
            Target = "all",
            CreatedAt = DateTime.Now,
            RequiresAcknowledgement = true
        };
        File.WriteAllText(Path.Combine(folder, $"{msg.MessageId}.json"), JsonHelper.Serialize(msg));
        return msg;
    }

    private string AllInbox => Path.Combine(_testDir, "inbox", "all");
    private string UserInbox
    {
        get
        {
            var s = _session.GetCurrentSession();
            return Path.Combine(_testDir, "inbox", "users", $"{s.Domain}_{s.Username}");
        }
    }
    private string ComputerInbox
    {
        get
        {
            var s = _session.GetCurrentSession();
            return Path.Combine(_testDir, "inbox", "computers", s.ComputerName);
        }
    }

    /// <summary>
    /// Polls a condition until it becomes true or the timeout elapses, instead of
    /// sleeping a fixed amount. Keeps timing-sensitive tests stable on slow CI.
    /// </summary>
    private static bool WaitUntil(Func<bool> condition, int timeoutMs = 2000)
    {
        var spent = 0;
        while (spent < timeoutMs)
        {
            if (condition()) return true;
            Thread.Sleep(20);
            spent += 20;
        }
        return condition();
    }

    // ---------------------------------------------------------------------
    // Lifecycle
    // ---------------------------------------------------------------------

    [Fact]
    public void Start_And_Stop_DoNotThrow()
    {
        var ex = Record.Exception(() =>
        {
            _receiver.Start();
            Thread.Sleep(200);
            _receiver.Stop();
        });
        Assert.Null(ex);
    }

    [Fact]
    public void Stop_WithoutStart_DoesNotThrow()
    {
        var ex = Record.Exception(() => _receiver.Stop());
        Assert.Null(ex);
    }

    [Fact]
    public void Stop_CalledTwice_DoesNotThrow()
    {
        _receiver.Start();
        var ex = Record.Exception(() =>
        {
            _receiver.Stop();
            _receiver.Stop();
        });
        Assert.Null(ex);
    }

    [Fact]
    public void Start_WhenServerPathMissing_DoesNotThrow()
    {
        _settings.Settings.ServerPath = Path.Combine(_testDir, "does-not-exist");
        var received = new List<AlertMessage>();
        _receiver.MessageReceived += received.Add;

        var ex = Record.Exception(() =>
        {
            _receiver.Start();
            Thread.Sleep(200);
            _receiver.Stop();
        });

        Assert.Null(ex);
        Assert.Empty(received);
    }

    // ---------------------------------------------------------------------
    // Reception across the three inbox types
    // ---------------------------------------------------------------------

    [Fact]
    public void MessageReceived_FiredForNewMessageInAllInbox()
    {
        var received = new List<AlertMessage>();
        _receiver.MessageReceived += received.Add;

        WriteMessageToInbox(AllInbox, "broadcast message");

        _receiver.Start();
        WaitUntil(() => received.Count >= 1);
        _receiver.Stop();

        Assert.Single(received);
        Assert.Equal("broadcast message", received[0].Body);
    }

    [Fact]
    public void MessageReceived_FiredForUserInbox()
    {
        var received = new List<AlertMessage>();
        _receiver.MessageReceived += received.Add;

        WriteMessageToInbox(UserInbox, "user specific");
        _receiver.Start();
        WaitUntil(() => received.Count >= 1);
        _receiver.Stop();

        Assert.Single(received);
        Assert.Equal("user specific", received[0].Body);
    }

    [Fact]
    public void MessageReceived_FiredForComputerInbox()
    {
        var received = new List<AlertMessage>();
        _receiver.MessageReceived += received.Add;

        WriteMessageToInbox(ComputerInbox, "computer specific");
        _receiver.Start();
        WaitUntil(() => received.Count >= 1);
        _receiver.Stop();

        Assert.Single(received);
        Assert.Equal("computer specific", received[0].Body);
    }

    [Fact]
    public void MessageReceived_FiredForMessagesInAllThreeInboxes()
    {
        var received = new List<AlertMessage>();
        _receiver.MessageReceived += received.Add;

        WriteMessageToInbox(AllInbox, "to-all");
        WriteMessageToInbox(UserInbox, "to-user");
        WriteMessageToInbox(ComputerInbox, "to-computer");

        _receiver.Start();
        WaitUntil(() => received.Count >= 3);
        _receiver.Stop();

        Assert.Equal(3, received.Count);
        var bodies = received.Select(m => m.Body).ToHashSet();
        Assert.Contains("to-all", bodies);
        Assert.Contains("to-user", bodies);
        Assert.Contains("to-computer", bodies);
    }

    [Fact]
    public void MultipleMessages_AllReceivedOnce()
    {
        var received = new List<AlertMessage>();
        _receiver.MessageReceived += received.Add;

        for (int i = 0; i < 10; i++)
            WriteMessageToInbox(AllInbox, $"msg-{i}");

        _receiver.Start();
        WaitUntil(() => received.Count >= 10);
        Thread.Sleep(300); // give a couple more poll cycles a chance to misbehave
        _receiver.Stop();

        Assert.Equal(10, received.Count);
        Assert.Equal(10, received.Select(m => m.MessageId).Distinct().Count());
    }

    [Fact]
    public void NoMessages_NothingReceived()
    {
        var received = new List<AlertMessage>();
        _receiver.MessageReceived += received.Add;

        _receiver.Start();
        Thread.Sleep(300);
        _receiver.Stop();

        Assert.Empty(received);
    }

    [Fact]
    public void MessageArrivingAfterStart_IsReceived()
    {
        var received = new List<AlertMessage>();
        _receiver.MessageReceived += received.Add;

        _receiver.Start();
        Thread.Sleep(100);
        WriteMessageToInbox(AllInbox, "late arrival");

        WaitUntil(() => received.Count >= 1);
        _receiver.Stop();

        Assert.Single(received);
        Assert.Equal("late arrival", received[0].Body);
    }

    // ---------------------------------------------------------------------
    // The core bug: a message must be shown exactly once and never re-open
    // after acknowledgement, even with the poll timer running continuously.
    // ---------------------------------------------------------------------

    [Fact]
    public void SameMessage_NotShownTwice_AcrossManyPolls()
    {
        var received = new List<AlertMessage>();
        _receiver.MessageReceived += received.Add;

        WriteMessageToInbox(AllInbox);

        _receiver.Start();
        // Let the 1-second poll fire several times. The file is never deleted,
        // so without the guard it would re-fire every cycle.
        Thread.Sleep(3500);
        _receiver.Stop();

        Assert.Single(received);
    }

    [Fact]
    public void MessageReceived_NotFiredTwiceForSameMessage()
    {
        var received = new List<AlertMessage>();
        _receiver.MessageReceived += received.Add;

        var msg = WriteMessageToInbox(AllInbox);

        _receiver.Start();
        WaitUntil(() => received.Count >= 1);

        _receiver.MarkAsRead(msg.MessageId);
        Thread.Sleep(1500); // multiple polls after ack
        _receiver.Stop();

        Assert.Single(received);
    }

    [Fact]
    public void AfterMarkAsRead_MessageNeverReopens_StressPolls()
    {
        // Reproduces the "had to press HO LETTO 15 times" report: ack the message,
        // then keep polling. The file on the share is still present the whole time.
        var received = new List<AlertMessage>();
        _receiver.MessageReceived += received.Add;

        var msg = WriteMessageToInbox(AllInbox, "single message to one person");

        _receiver.Start();
        WaitUntil(() => received.Count >= 1);
        Assert.Single(received);

        _receiver.MarkAsRead(msg.MessageId);

        // Five full poll cycles must not produce a single re-show.
        Thread.Sleep(5000);
        _receiver.Stop();

        Assert.Single(received);
        Assert.True(File.Exists(Path.Combine(AllInbox, $"{msg.MessageId}.json")),
            "the message file is intentionally never deleted; the guard must handle that");
    }

    [Fact]
    public void MarkAsRead_BeforeMessageEverPolled_PreventsShow()
    {
        // If the read-state already knows the id (e.g. acked on another poll path),
        // the message must not surface at all.
        var received = new List<AlertMessage>();
        _receiver.MessageReceived += received.Add;

        var msg = WriteMessageToInbox(AllInbox);
        _receiver.MarkAsRead(msg.MessageId);

        _receiver.Start();
        Thread.Sleep(1500);
        _receiver.Stop();

        Assert.Empty(received);
    }

    // ---------------------------------------------------------------------
    // Persistence of read-state
    // ---------------------------------------------------------------------

    [Fact]
    public void MarkAsRead_WritesReadStateToLocalFileSynchronously()
    {
        var msg = WriteMessageToInbox(AllInbox);

        _receiver.Start();
        WaitUntil(() => File.Exists(Path.Combine(_testDir, "history", "shown.jsonl")));
        _receiver.MarkAsRead(msg.MessageId);

        // The local read-state file must be on disk immediately, no Sleep needed,
        // because MarkAsRead persists it synchronously.
        var readStatePath = Path.Combine(_testDir, "read_messages.json");
        Assert.True(File.Exists(readStatePath));
        Assert.Contains(msg.MessageId, File.ReadAllText(readStatePath));
        _receiver.Stop();
    }

    [Fact]
    public void MarkAsRead_WritesToShownAndReadHistory()
    {
        var msg = WriteMessageToInbox(AllInbox);

        _receiver.Start();
        WaitUntil(() => File.Exists(Path.Combine(_testDir, "history", "shown.jsonl")));
        _receiver.MarkAsRead(msg.MessageId);

        var shownPath = Path.Combine(_testDir, "history", "shown.jsonl");
        var readPath = Path.Combine(_testDir, "history", "read.jsonl");

        Assert.True(File.Exists(shownPath));
        // RecordRead runs on a background thread; wait for it.
        Assert.True(WaitUntil(() => File.Exists(readPath) && File.ReadAllText(readPath).Contains(msg.MessageId)),
            "read history should be written shortly after MarkAsRead");
        _receiver.Stop();
    }

    [Fact]
    public void MarkAsRead_PersistsAcrossNewInstance()
    {
        var msg = WriteMessageToInbox(AllInbox);

        _receiver.Start();
        WaitUntil(() => File.Exists(Path.Combine(_testDir, "history", "shown.jsonl")));
        _receiver.MarkAsRead(msg.MessageId);
        _receiver.Stop();

        var receiver2 = new MessageReceiverService(_settings, _session, _history);
        var received = new List<AlertMessage>();
        receiver2.MessageReceived += received.Add;
        receiver2.Start();
        Thread.Sleep(1500);
        receiver2.Stop();

        Assert.Empty(received);
    }

    [Fact]
    public void MarkAsRead_PersistsImmediately_SurvivesAbruptShutdown()
    {
        // Simulate: user clicks HO LETTO then the process dies instantly (Stop
        // immediately, no grace period). The read-state must already be on disk.
        var msg = WriteMessageToInbox(AllInbox);

        _receiver.Start();
        WaitUntil(() => File.Exists(Path.Combine(_testDir, "history", "shown.jsonl")));
        _receiver.MarkAsRead(msg.MessageId);
        _receiver.Stop(); // no sleep — mimics an immediate exit

        var receiver2 = new MessageReceiverService(_settings, _session, _history);
        var received = new List<AlertMessage>();
        receiver2.MessageReceived += received.Add;
        receiver2.Start();
        Thread.Sleep(1500);
        receiver2.Stop();

        Assert.Empty(received);
    }

    [Fact]
    public void MarkAsRead_AccumulatesMultipleIds()
    {
        var m1 = WriteMessageToInbox(AllInbox, "one");
        var m2 = WriteMessageToInbox(AllInbox, "two");

        _receiver.Start();
        WaitUntil(() => File.Exists(Path.Combine(_testDir, "history", "shown.jsonl")));
        _receiver.MarkAsRead(m1.MessageId);
        _receiver.MarkAsRead(m2.MessageId);
        _receiver.Stop();

        var readState = File.ReadAllText(Path.Combine(_testDir, "read_messages.json"));
        Assert.Contains(m1.MessageId, readState);
        Assert.Contains(m2.MessageId, readState);
    }

    // ---------------------------------------------------------------------
    // Idempotency and edge cases of MarkAsRead
    // ---------------------------------------------------------------------

    [Fact]
    public void MarkAsRead_CalledManyTimes_IsIdempotent_AndNeverReopens()
    {
        // Even if the UI fires MarkAsRead repeatedly (double clicks before the
        // window closes), behaviour must stay correct.
        var received = new List<AlertMessage>();
        _receiver.MessageReceived += received.Add;

        var msg = WriteMessageToInbox(AllInbox);
        _receiver.Start();
        WaitUntil(() => received.Count >= 1);

        for (int i = 0; i < 15; i++)
            _receiver.MarkAsRead(msg.MessageId);

        Thread.Sleep(2500);
        _receiver.Stop();

        Assert.Single(received);
    }

    [Fact]
    public void MarkAsRead_UnknownId_DoesNotThrow()
    {
        var ex = Record.Exception(() => _receiver.MarkAsRead("never-seen-id"));
        Assert.Null(ex);
    }

    [Fact]
    public void MarkAsRead_BeforeStart_DoesNotThrow()
    {
        var ex = Record.Exception(() => _receiver.MarkAsRead(AlertMessage.GenerateId()));
        Assert.Null(ex);
    }

    // ---------------------------------------------------------------------
    // Concurrency: the read-state set is touched by both the poll timer and
    // the UI thread. Hammer it to flush out races / corruption.
    // ---------------------------------------------------------------------

    [Fact]
    public void ConcurrentMarkAsRead_FromManyThreads_DoesNotThrowOrCorrupt()
    {
        var ids = Enumerable.Range(0, 200).Select(_ => AlertMessage.GenerateId()).ToList();

        var ex = Record.Exception(() =>
            Parallel.ForEach(ids, id => _receiver.MarkAsRead(id)));

        Assert.Null(ex);

        // Give background writers a moment, then verify every id persisted.
        WaitUntil(() =>
        {
            if (!File.Exists(Path.Combine(_testDir, "read_messages.json"))) return false;
            var text = File.ReadAllText(Path.Combine(_testDir, "read_messages.json"));
            return ids.All(text.Contains);
        });

        var finalText = File.ReadAllText(Path.Combine(_testDir, "read_messages.json"));
        foreach (var id in ids)
            Assert.Contains(id, finalText);
    }

    [Fact]
    public async Task MarkAsRead_ConcurrentWithActivePolling_NoDuplicateShows()
    {
        // The hardest race: poll timer running at max frequency while we ack.
        var received = new System.Collections.Concurrent.ConcurrentBag<AlertMessage>();
        _receiver.MessageReceived += received.Add;

        var msg = WriteMessageToInbox(AllInbox);
        _receiver.Start();
        WaitUntil(() => received.Count >= 1);

        // Ack repeatedly from a separate thread while polling keeps running.
        await Task.Run(() =>
        {
            for (int i = 0; i < 30; i++)
            {
                _receiver.MarkAsRead(msg.MessageId);
                Thread.Sleep(10);
            }
        });

        Thread.Sleep(2500);
        _receiver.Stop();

        Assert.Single(received);
    }

    // ---------------------------------------------------------------------
    // Robustness against bad input on the share
    // ---------------------------------------------------------------------

    [Fact]
    public void MalformedMessageFile_IsIgnored()
    {
        Directory.CreateDirectory(AllInbox);
        File.WriteAllText(Path.Combine(AllInbox, "bad.json"), "{ not json ]]]");

        var received = new List<AlertMessage>();
        _receiver.MessageReceived += received.Add;
        _receiver.Start();
        Thread.Sleep(500);
        _receiver.Stop();

        Assert.Empty(received);
    }

    [Fact]
    public void MalformedFile_DoesNotBlockValidMessages()
    {
        Directory.CreateDirectory(AllInbox);
        File.WriteAllText(Path.Combine(AllInbox, "bad.json"), "}{ broken");
        WriteMessageToInbox(AllInbox, "i am valid");

        var received = new List<AlertMessage>();
        _receiver.MessageReceived += received.Add;
        _receiver.Start();
        WaitUntil(() => received.Count >= 1);
        _receiver.Stop();

        Assert.Single(received);
        Assert.Equal("i am valid", received[0].Body);
    }

    [Fact]
    public void EmptyJsonFile_IsIgnored()
    {
        Directory.CreateDirectory(AllInbox);
        File.WriteAllText(Path.Combine(AllInbox, "empty.json"), "");

        var received = new List<AlertMessage>();
        _receiver.MessageReceived += received.Add;
        _receiver.Start();
        Thread.Sleep(500);
        _receiver.Stop();

        Assert.Empty(received);
    }

    [Fact]
    public void JsonNullLiteral_IsIgnored()
    {
        Directory.CreateDirectory(AllInbox);
        File.WriteAllText(Path.Combine(AllInbox, "null.json"), "null");

        var received = new List<AlertMessage>();
        _receiver.MessageReceived += received.Add;
        _receiver.Start();
        Thread.Sleep(500);
        _receiver.Stop();

        Assert.Empty(received);
    }

    [Fact]
    public void NonJsonExtensionFiles_AreIgnored()
    {
        Directory.CreateDirectory(AllInbox);
        File.WriteAllText(Path.Combine(AllInbox, "readme.txt"), "not a message");
        File.WriteAllText(Path.Combine(AllInbox, "data.xml"), "<x/>");

        var received = new List<AlertMessage>();
        _receiver.MessageReceived += received.Add;
        _receiver.Start();
        Thread.Sleep(500);
        _receiver.Stop();

        Assert.Empty(received);
    }

    [Fact]
    public void CorruptReadStateFile_OnStart_IsToleratedAndMessageShows()
    {
        // A garbage read_messages.json must not crash startup nor suppress messages.
        var readStatePath = Path.Combine(_testDir, "read_messages.json");
        File.WriteAllText(readStatePath, "{ totally broken ]]");

        var received = new List<AlertMessage>();
        _receiver.MessageReceived += received.Add;
        WriteMessageToInbox(AllInbox, "should still show");

        var ex = Record.Exception(() =>
        {
            _receiver.Start();
            WaitUntil(() => received.Count >= 1);
            _receiver.Stop();
        });

        Assert.Null(ex);
        Assert.Single(received);
    }

    [Fact]
    public void PreexistingReadState_SuppressesMatchingMessage()
    {
        var msg = WriteMessageToInbox(AllInbox);

        // Seed read-state on disk as if a previous run had already acked it.
        var readStatePath = Path.Combine(_testDir, "read_messages.json");
        File.WriteAllText(readStatePath, JsonHelper.Serialize(new List<string> { msg.MessageId }));

        var received = new List<AlertMessage>();
        _receiver.MessageReceived += received.Add;
        _receiver.Start();
        Thread.Sleep(1500);
        _receiver.Stop();

        Assert.Empty(received);
    }

    // ---------------------------------------------------------------------
    // Per-user read-state: legacy import, cross-process merge, corruption.
    // ---------------------------------------------------------------------

    [Fact]
    public void LegacyMachineWideReadState_IsImportedOnFirstStart()
    {
        var msg = WriteMessageToInbox(AllInbox);
        File.WriteAllText(Path.Combine(_testDir, "legacy_read_messages.json"),
            JsonHelper.Serialize(new List<string> { msg.MessageId }));

        var received = new List<AlertMessage>();
        _receiver.MessageReceived += received.Add;
        _receiver.Start();
        Thread.Sleep(500);
        _receiver.Stop();

        Assert.Empty(received);
        Assert.Contains(msg.MessageId, File.ReadAllText(Path.Combine(_testDir, "read_messages.json")));
    }

    [Fact]
    public void LegacyReadState_IsIgnoredOncePerUserStateExists()
    {
        var msg = WriteMessageToInbox(AllInbox);
        File.WriteAllText(Path.Combine(_testDir, "read_messages.json"), "[]");
        File.WriteAllText(Path.Combine(_testDir, "legacy_read_messages.json"),
            JsonHelper.Serialize(new List<string> { msg.MessageId }));

        var received = new List<AlertMessage>();
        _receiver.MessageReceived += received.Add;
        _receiver.Start();
        WaitUntil(() => received.Count >= 1);
        _receiver.Stop();

        Assert.Single(received);
    }

    [Fact]
    public void TwoInstancesOfSameUser_AcksAreMergedNotLost()
    {
        // Simulates two RDP sessions of the same user sharing the per-user file.
        var other = new MessageReceiverService(_settings, _session, _history);
        _receiver.Start();
        other.Start();

        _receiver.MarkAsRead("id-from-session-1");
        other.MarkAsRead("id-from-session-2");
        _receiver.MarkAsRead("id-from-session-1b");

        other.Stop();
        var text = File.ReadAllText(Path.Combine(_testDir, "read_messages.json"));
        Assert.Contains("id-from-session-1", text);
        Assert.Contains("id-from-session-2", text);
        Assert.Contains("id-from-session-1b", text);
    }

    [Fact]
    public void CorruptReadStateFile_IsBackedUpBeforeBeingReplaced()
    {
        var readStatePath = Path.Combine(_testDir, "read_messages.json");
        File.WriteAllText(readStatePath, "{ totally broken ]]");

        _receiver.Start();
        _receiver.MarkAsRead("fresh-id");
        _receiver.Stop();

        Assert.True(File.Exists(readStatePath + ".corrupt"));
        Assert.Contains("fresh-id", File.ReadAllText(readStatePath));
    }

    [Fact]
    public void HandlerFailure_ReleasesMessageForRetry()
    {
        WriteMessageToInbox(AllInbox);
        var calls = 0;
        _receiver.MessageReceived += _ =>
        {
            if (Interlocked.Increment(ref calls) == 1)
                throw new InvalidOperationException("UI not ready");
        };

        _receiver.Start();
        var retried = WaitUntil(() => Volatile.Read(ref calls) >= 2, 4000);
        _receiver.Stop();

        Assert.True(retried);
    }
}
