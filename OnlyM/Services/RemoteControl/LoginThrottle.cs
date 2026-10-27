using System;
using System.Collections.Generic;
using System.Linq;

namespace OnlyM.Services.RemoteControl;

/// <summary>
/// Per-client PIN attempt limiter: after <see cref="MaxFailures"/> wrong PINs a client is locked out
/// for <see cref="Lockout"/>. Stale entries are pruned and the table is bounded, so a flood of distinct
/// clients cannot grow it without limit. Thread-safe.
/// </summary>
internal sealed class LoginThrottle
{
    public const int DefaultMaxFailures = 5;
    public const int DefaultMaxEntries = 1000;

    private static readonly TimeSpan PruneInterval = TimeSpan.FromMinutes(1);

    private readonly object _lock = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly Func<DateTime> _utcNow;
    private readonly TimeSpan _failureWindow;
    private readonly int _maxEntries;
    private DateTime _lastPruneUtc = DateTime.MinValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoginThrottle"/> class.
    /// </summary>
    /// <param name="utcNow">Clock (for tests); defaults to <see cref="DateTime.UtcNow"/>.</param>
    /// <param name="maxFailures">Wrong PINs allowed before a lockout.</param>
    /// <param name="lockout">Lockout duration (default 60 s).</param>
    /// <param name="failureWindow">Failures older than this are forgotten (default 15 min).</param>
    /// <param name="maxEntries">Maximum number of clients tracked.</param>
    public LoginThrottle(
        Func<DateTime>? utcNow = null,
        int maxFailures = DefaultMaxFailures,
        TimeSpan? lockout = null,
        TimeSpan? failureWindow = null,
        int maxEntries = DefaultMaxEntries)
    {
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
        MaxFailures = Math.Max(1, maxFailures);
        Lockout = lockout ?? TimeSpan.FromSeconds(60);
        _failureWindow = failureWindow ?? TimeSpan.FromMinutes(15);
        _maxEntries = Math.Max(1, maxEntries);
    }

    public int MaxFailures { get; }

    public TimeSpan Lockout { get; }

    /// <summary>
    /// Gets the number of clients currently tracked.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _entries.Count;
            }
        }
    }

    public bool IsLockedOut(string key, out TimeSpan retryAfter)
    {
        lock (_lock)
        {
            var now = _utcNow();
            if (_entries.TryGetValue(key, out var e) && e.LockedUntilUtc > now)
            {
                retryAfter = e.LockedUntilUtc - now;
                return true;
            }

            retryAfter = TimeSpan.Zero;
            return false;
        }
    }

    public FailureResult RegisterFailure(string key)
    {
        lock (_lock)
        {
            var now = _utcNow();
            PruneIfDue(now);

            if (!_entries.TryGetValue(key, out var e) || IsStale(e, now))
            {
                _entries.Remove(key);
                MakeRoom(now);
                e = new Entry();
                _entries[key] = e;
            }

            if (e.LockedUntilUtc > now)
            {
                return new FailureResult(true, 0, e.LockedUntilUtc - now);
            }

            e.Failures++;
            e.LastFailureUtc = now;
            if (e.Failures >= MaxFailures)
            {
                e.Failures = 0;
                e.LockedUntilUtc = now + Lockout;
                return new FailureResult(true, 0, Lockout);
            }

            return new FailureResult(false, MaxFailures - e.Failures, TimeSpan.Zero);
        }
    }

    public void RegisterSuccess(string key)
    {
        lock (_lock)
        {
            _entries.Remove(key);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
        }
    }

    private bool IsStale(Entry e, DateTime now) =>
        e.LockedUntilUtc <= now && now - e.LastFailureUtc >= _failureWindow;

    private void PruneIfDue(DateTime now)
    {
        if (now - _lastPruneUtc < PruneInterval && _entries.Count < _maxEntries)
        {
            return;
        }

        _lastPruneUtc = now;
        foreach (var key in _entries.Where(kv => IsStale(kv.Value, now)).Select(kv => kv.Key).ToList())
        {
            _entries.Remove(key);
        }
    }

    private void MakeRoom(DateTime now)
    {
        if (_entries.Count < _maxEntries)
        {
            return;
        }

        // Still full after pruning: evict the oldest entries, preferring ones that are not locked out
        var victims = _entries
            .OrderBy(kv => kv.Value.LockedUntilUtc > now ? 1 : 0)
            .ThenBy(kv => kv.Value.LastFailureUtc)
            .Take(_entries.Count - _maxEntries + 1)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var key in victims)
        {
            _entries.Remove(key);
        }
    }

    internal readonly struct FailureResult
    {
        public FailureResult(bool lockedOut, int attemptsLeft, TimeSpan retryAfter)
        {
            LockedOut = lockedOut;
            AttemptsLeft = attemptsLeft;
            RetryAfter = retryAfter;
        }

        public bool LockedOut { get; }

        public int AttemptsLeft { get; }

        public TimeSpan RetryAfter { get; }
    }

    private sealed class Entry
    {
        public int Failures { get; set; }

        public DateTime LastFailureUtc { get; set; }

        public DateTime LockedUntilUtc { get; set; }
    }
}
