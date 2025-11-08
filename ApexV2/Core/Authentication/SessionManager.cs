namespace ApexV2.Core.Authentication;

using ApexV2.Core.Database;
using ApexV2.Core.Logging;

public class SessionInfo
{
    public string Token { get; init; } = string.Empty;
    public Guid UserPublicId { get; init; }
    public string Username { get; init; } = string.Empty;
    public DateTime CreatedUtc { get; init; }
    public DateTime LastActivityUtc { get; set; }
    public DateTime ExpiresUtc { get; set; }
    public string SecurityStamp { get; init; } = string.Empty;
}

public class SessionManager
{
    private readonly Dictionary<string, SessionInfo> _sessions = new();
    private readonly TimeSpan _idleTimeout = TimeSpan.FromMinutes(60);
    private readonly TimeSpan _absoluteTimeout = TimeSpan.FromHours(8);
    private readonly Logger _logger;

    public SessionManager(LogManager logManager)
    {
        _logger = logManager.GetLogger("SessionManager");
    }

    public SessionInfo CreateSession(UserEntity user)
    {
        var token = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;
        var session = new SessionInfo
        {
            Token = token,
            UserPublicId = user.PublicId,
            Username = user.Username,
            CreatedUtc = now,
            LastActivityUtc = now,
            ExpiresUtc = now.Add(_idleTimeout),
            SecurityStamp = user.SecurityStamp
        };
        _sessions[token] = session;
        _logger.Info("Session created", new Dictionary<string, object?>{{"user", user.Username}});
        return session;
    }

    public SessionInfo? Validate(string token, string currentSecurityStamp)
    {
        if (!_sessions.TryGetValue(token, out var session)) return null;
        if (session.SecurityStamp != currentSecurityStamp) { _sessions.Remove(token); return null; }
        var now = DateTime.UtcNow;
        if (now - session.CreatedUtc > _absoluteTimeout) { _sessions.Remove(token); return null; }
        if (now > session.ExpiresUtc) { _sessions.Remove(token); return null; }
        session.LastActivityUtc = now;
        session.ExpiresUtc = now.Add(_idleTimeout);
        return session;
    }

    public void Invalidate(string token)
    {
        if (_sessions.Remove(token, out var session))
            _logger.Info("Session invalidated", new Dictionary<string, object?>{{"user", session.Username}});
    }

    public void InvalidateAllForUser(Guid userPublicId)
    {
        var toRemove = _sessions.Where(kv => kv.Value.UserPublicId == userPublicId).Select(kv => kv.Key).ToList();
        foreach (var key in toRemove) _sessions.Remove(key);
        if (toRemove.Count > 0) _logger.Info("All sessions invalidated", new Dictionary<string, object?>{{"userPublicId", userPublicId}});
    }
}
