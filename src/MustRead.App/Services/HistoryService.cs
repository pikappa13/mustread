using MustRead.App.Models;
using MustRead.App.Utils;

namespace MustRead.App.Services;

public class HistoryService
{
    private readonly SettingsService _settings;

    public HistoryService(SettingsService settings)
    {
        _settings = settings;
    }

    public void RecordSent(AlertMessage msg, ClientInfo sender)
    {
        var item = new SentHistoryItem
        {
            MessageId = msg.MessageId,
            Sender = $@"{sender.Domain}\{sender.Username}",
            SenderComputer = sender.ComputerName,
            TargetType = msg.TargetType,
            Target = msg.Target,
            Body = msg.Body,
            SentAt = DateTime.Now
        };
        Append("sent.jsonl", item);
    }

    public void RecordShown(string messageId, ClientInfo receiver)
    {
        var item = new ShownHistoryItem
        {
            MessageId = messageId,
            ShownTo = $@"{receiver.Domain}\{receiver.Username}",
            ShownOnComputer = receiver.ComputerName,
            SessionType = receiver.SessionType,
            ShownAt = DateTime.Now
        };
        Append("shown.jsonl", item);
    }

    public void RecordRead(string messageId, ClientInfo receiver)
    {
        var item = new ReadHistoryItem
        {
            MessageId = messageId,
            ReadBy = $@"{receiver.Domain}\{receiver.Username}",
            ReadOnComputer = receiver.ComputerName,
            SessionType = receiver.SessionType,
            ReadAt = DateTime.Now
        };
        Append("read.jsonl", item);
    }

    private void Append<T>(string fileName, T item)
    {
        try
        {
            var path = _settings.GetHistoryPath(fileName);
            var line = JsonHelper.Serialize(item)
                .Replace(Environment.NewLine, " ")
                .Replace("\n", " ");
            FileLockHelper.AppendLine(path, line);
        }
        catch
        {
            // History write failure is non-fatal
        }
    }
}
