using System;
using System.Collections.Generic;

namespace OnlyM.Services.RemoteControl;

/// <summary>
/// Global limit on wrong PINs from all clients together, as a backstop for attackers that spread their
/// guesses over many addresses. At most <see cref="Budget"/> failures are allowed in any rolling
/// <see cref="Window"/>; once the budget is used up, login attempts are refused (not checked) until the
/// oldest failure ages out. The suggested Retry-After starts at <see cref="InitialDelay"/> and doubles for
/// each refused attempt while the pressure continues, capped at <see cref="MaxDelay"/> and at the time
/// until the next failure ages out. Only failures count: successful logins and other requests do not.
/// Thread-safe.
/// </summary>
internal sealed class GlobalLoginBackstop
{
    public const int DefaultBudget = 20;

    private readonly object _lock = new();
    private readonly Queue<DateTime> _failures = new();
    private readonly Func<DateTime> _utcNow;
    private int _pressure;

    /// <param name="utcNow">Clock (for tests); defaults to <see cref="DateTime.UtcNow"/>.</param>
    /// <param name="budget">Failures allowed per window (default 20).</param>
    /// <param name="window">Rolling window (default 10 min).</param>
    /// <param name="initialDelay">First Retry-After when throttling (default 5 s).</param>
    /// <param name="maxDelay">Retry-After cap (default 60 s).</param>
    public GlobalLoginBackstop(
        Func<DateTime>? utcNow = null,
        int budget = DefaultBudget,
        TimeSpan? window = null,
        TimeSpan? initialDelay = null,
        TimeSpan? maxDelay = null)
    {
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
        Budget = Math.Max(1, budget);
        Window = window ?? TimeSpan.FromMinutes(10);
        InitialDelay = initialDelay ?? TimeSpan.FromSeconds(5);
        MaxDelay = maxDelay ?? TimeSpan.FromSeconds(60);
    }

    public int Budget { get; }

    public TimeSpan Window { get; }

    public TimeSpan InitialDelay { get; }

    public TimeSpan MaxDelay { get; }

    /// <summary>
    /// Gets the number of failures in the current window.
    /// </summary>
    public int FailureCount
    {
        get
        {
            lock (_lock)
            {
                Drain(_utcNow());
                return _failures.Count;
            }
        }
    }

    /// <summary>
    /// Checks whether a login attempt may go ahead. When the budget is used up, returns true with the
    /// suggested wait; each such refusal raises the next suggested wait.
    /// </summary>
    public bool IsThrottled(out TimeSpan retryAfter)
    {
        lock (_lock)
        {
            var now = _utcNow();
            Drain(now);
            if (_failures.Count < Budget)
            {
                _pressure = 0;
                retryAfter = TimeSpan.Zero;
                return false;
            }

            var delay = TimeSpan.FromTicks(Math.Min(MaxDelay.Ticks, InitialDelay.Ticks << Math.Min(_pressure, 20)));
            if (_pressure < 20)
            {
                _pressure++;
            }

            var untilDrain = _failures.Peek() + Window - now;
            retryAfter = delay < untilDrain ? delay : untilDrain;
            if (retryAfter < TimeSpan.FromSeconds(1))
            {
                retryAfter = TimeSpan.FromSeconds(1);
            }

            return true;
        }
    }

    public void RegisterFailure()
    {
        lock (_lock)
        {
            var now = _utcNow();
            Drain(now);
            _failures.Enqueue(now);

            // Only the newest Budget failures matter (concurrent attempts can overshoot slightly)
            while (_failures.Count > Budget)
            {
                _failures.Dequeue();
            }
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _failures.Clear();
            _pressure = 0;
        }
    }

    private void Drain(DateTime now)
    {
        while (_failures.Count > 0 && now - _failures.Peek() >= Window)
        {
            _failures.Dequeue();
        }
    }
}
