using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace OnlyM.Services.RemoteControl;

/// <summary>
/// Remote control login sessions. A session expires after <see cref="IdleTimeout"/> without requests
/// (sliding) and in any case <see cref="MaxAge"/> after login (absolute). Expired sessions are pruned
/// on access and the number of sessions is bounded (oldest evicted first). Thread-safe.
/// </summary>
internal sealed class RemoteSessionStore
{
    public const int DefaultMaxSessions = 200;

    private readonly object _lock = new();
    private readonly Dictionary<string, Session> _sessions = new(StringComparer.Ordinal);
    private readonly Func<DateTime> _utcNow;
    private readonly int _maxSessions;

    /// <param name="utcNow">Clock (for tests); defaults to <see cref="DateTime.UtcNow"/>.</param>
    /// <param name="idleTimeout">Sliding idle timeout (default 30 min).</param>
    /// <param name="maxAge">Absolute session lifetime (default 12 h).</param>
    /// <param name="maxSessions">Maximum number of concurrent sessions.</param>
    public RemoteSessionStore(
        Func<DateTime>? utcNow = null,
        TimeSpan? idleTimeout = null,
        TimeSpan? maxAge = null,
        int maxSessions = DefaultMaxSessions)
    {
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
        IdleTimeout = idleTimeout ?? TimeSpan.FromMinutes(30);
        MaxAge = maxAge ?? TimeSpan.FromHours(12);
        _maxSessions = Math.Max(1, maxSessions);
    }

    public TimeSpan IdleTimeout { get; }

    public TimeSpan MaxAge { get; }

    public int Count
    {
        get
        {
            lock (_lock)
            {
                PruneExpired(_utcNow());
                return _sessions.Count;
            }
        }
    }

    /// <summary>
    /// Creates a session and returns its (random, 256-bit) token.
    /// </summary>
    public string Create()
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        lock (_lock)
        {
            var now = _utcNow();
            PruneExpired(now);
            while (_sessions.Count >= _maxSessions)
            {
                var oldest = _sessions.MinBy(kv => kv.Value.LastSeenUtc).Key;
                _sessions.Remove(oldest);
            }

            _sessions[token] = new Session(now) { LastSeenUtc = now };
        }

        return token;
    }

    /// <summary>
    /// Returns true if the token belongs to a live session, and extends its idle timeout.
    /// </summary>
    public bool TryValidate(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        lock (_lock)
        {
            var now = _utcNow();
            PruneExpired(now);
            if (!_sessions.TryGetValue(token, out var session))
            {
                return false;
            }

            session.LastSeenUtc = now;
            return true;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _sessions.Clear();
        }
    }

    private bool IsExpired(Session s, DateTime now) =>
        now - s.LastSeenUtc >= IdleTimeout || now - s.CreatedUtc >= MaxAge;

    private void PruneExpired(DateTime now)
    {
        foreach (var key in _sessions.Where(kv => IsExpired(kv.Value, now)).Select(kv => kv.Key).ToList())
        {
            _sessions.Remove(key);
        }
    }

    private sealed class Session
    {
        public Session(DateTime createdUtc) => CreatedUtc = createdUtc;

        public DateTime CreatedUtc { get; }

        public DateTime LastSeenUtc { get; set; }
    }
}
