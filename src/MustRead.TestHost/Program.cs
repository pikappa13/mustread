using MustRead.App.Services;
using MustRead.App.Utils;

// Usage (all paths absolute):
//   append   <file> <count> <tag>                 append <count> lines with FileLockHelper
//   markread <baseDir> <shareDir> <prefix> <count> ack ids <prefix>-0..n with a fresh receiver
//   crash-after-ack <baseDir> <shareDir> <id>     ack one id, then terminate abruptly
//   record   <baseDir> <shareDir> <prefix> <count> queue + flush read records
// Exit code 0 = success.

var command = args.ElementAtOrDefault(0);
try
{
    switch (command)
    {
        case "append":
        {
            var file = args[1];
            var count = int.Parse(args[2]);
            for (var i = 0; i < count; i++)
                FileLockHelper.AppendLine(file, $"{{\"tag\":\"{args[3]}\",\"i\":{i}}}");
            return 0;
        }
        case "markread":
        {
            var receiver = CreateReceiver(args[1], args[2], out _);
            for (var i = 0; i < int.Parse(args[4]); i++)
                receiver.MarkAsRead($"{args[3]}-{i}");
            return 0;
        }
        case "crash-after-ack":
        {
            var receiver = CreateReceiver(args[1], args[2], out _);
            receiver.MarkAsRead(args[3]);
            // Simulates the process being killed right after the user clicked HO LETTO.
            Environment.FailFast("simulated crash");
            return 1;
        }
        case "record":
        {
            CreateReceiver(args[1], args[2], out var history);
            var client = new SessionInfoService().GetCurrentSession();
            for (var i = 0; i < int.Parse(args[4]); i++)
                history.RecordRead($"{args[3]}-{i}", client);
            return 0;
        }
        default:
            Console.Error.WriteLine($"Comando sconosciuto: {command}");
            return 2;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}

static MessageReceiverService CreateReceiver(string baseDir, string shareDir, out HistoryService history)
{
    Log.Directory = Path.Combine(baseDir, "logs");
    var settings = new HostSettingsService(baseDir);
    settings.Settings.ServerPath = shareDir;
    history = new HistoryService(settings);
    return new MessageReceiverService(settings, new SessionInfoService(), history);
}

internal class HostSettingsService : SettingsService
{
    private readonly string _baseDir;
    public HostSettingsService(string baseDir) => _baseDir = baseDir;
    protected override string LocalSettingsPath => Path.Combine(_baseDir, "settings.json");
    protected override string LocalReadMessagesPath => Path.Combine(_baseDir, "read_messages.json");
    protected override string LegacyReadMessagesPath => Path.Combine(_baseDir, "legacy_read_messages.json");
    protected override string LocalOutboxPath => Path.Combine(_baseDir, "outbox");
}
