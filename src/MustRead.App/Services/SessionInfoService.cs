using MustRead.App.Models;

namespace MustRead.App.Services;

public class SessionInfoService
{
    public ClientInfo GetCurrentSession()
    {
        var domain = Environment.UserDomainName;
        var username = Environment.UserName;
        var computerName = Environment.MachineName;
        var sessionName = Environment.GetEnvironmentVariable("SESSIONNAME") ?? "Console";
        var sessionId = GetSessionId();
        var sessionType = DetectSessionType(sessionName);

        var clientId = $"{computerName}_{domain}_{username}_{sessionId}";

        return new ClientInfo
        {
            ClientId = clientId,
            Domain = domain,
            Username = username,
            ComputerName = computerName,
            SessionId = sessionId,
            SessionName = sessionName,
            SessionType = sessionType,
            LastSeen = DateTime.Now
        };
    }

    private static string DetectSessionType(string sessionName)
    {
        if (sessionName.Contains("RDP", StringComparison.OrdinalIgnoreCase))
            return "RDP";
        return "Local";
    }

    private static int GetSessionId()
    {
        try
        {
            return System.Diagnostics.Process.GetCurrentProcess().SessionId;
        }
        catch
        {
            return 0;
        }
    }
}
