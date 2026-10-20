namespace MustRead.App.Models;

public class AlertMessage
{
    public string MessageId { get; set; } = string.Empty;
    public string SenderDomain { get; set; } = string.Empty;
    public string SenderUsername { get; set; } = string.Empty;
    public string SenderComputer { get; set; } = string.Empty;
    public string TargetType { get; set; } = "User"; // User | Computer | All
    public string Target { get; set; } = string.Empty;
    public string Title { get; set; } = "MESSAGGIO IMPORTANTE";
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool RequiresAcknowledgement { get; set; } = true;

    /// <summary>After this time the message is no longer shown. Null: never expires.</summary>
    public DateTime? ExpiresAt { get; set; }

    public bool IsExpired(DateTime now) => ExpiresAt.HasValue && ExpiresAt.Value <= now;

    /// <summary>
    /// True when a "User" message is addressed to <paramref name="domain"/>\<paramref name="username"/>.
    /// Resolves the target exactly as <c>MessageSenderService.Send</c> does: a target
    /// without a domain belongs to the sender's domain. Other target types always match.
    /// </summary>
    public bool IsAddressedTo(string domain, string username)
    {
        if (TargetType != "User") return true;

        var parts = Target.Split('\\');
        var targetDomain = parts.Length == 2 ? parts[0] : SenderDomain;
        var targetUser = parts.Length == 2 ? parts[1] : Target;
        return string.Equals(targetDomain, domain, StringComparison.OrdinalIgnoreCase)
               && string.Equals(targetUser, username, StringComparison.OrdinalIgnoreCase);
    }

    public static string GenerateId()
    {
        var now = DateTime.Now;
        var suffix = Guid.NewGuid().ToString("N")[..12];
        return $"{now:yyyyMMdd-HHmmss}-{suffix}";
    }
}
