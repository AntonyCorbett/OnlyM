using System;

namespace OnlyM.Services.RemoteControl;

/// <summary>
/// All login rate limits together: per client (full IPv4 address or IPv6 /64, 5 failures), per IPv6 /48
/// (12 failures), each with a 60 s lockout, plus the <see cref="GlobalLoginBackstop"/>. Thread-safe.
/// </summary>
internal sealed class LoginGuard
{
    public const int Prefix48MaxFailures = 12;

    private readonly object _attemptLock = new();

    public LoginGuard(Func<DateTime>? utcNow = null)
    {
        Clients = new LoginThrottle(utcNow);
        Prefixes = new LoginThrottle(utcNow, Prefix48MaxFailures);
        Global = new GlobalLoginBackstop(utcNow);
    }

    internal enum Block
    {
        None,
        Client,
        Global,
    }

    public LoginThrottle Clients { get; }

    public LoginThrottle Prefixes { get; }

    public GlobalLoginBackstop Global { get; }

    /// <summary>
    /// Runs one login attempt as a single step: checks the limits, verifies the PIN and records the outcome
    /// under one lock. Requests are handled in parallel, so with separate <see cref="Check"/> and
    /// <see cref="RegisterFailure"/> calls a burst of simultaneous attempts could all pass the check before
    /// the first failure was recorded, and so get more guesses than the limits allow.
    /// </summary>
    /// <param name="keys">The client's throttling keys.</param>
    /// <param name="isCorrect">Verifies the PIN; only called when no limit blocks the attempt. Must be fast.</param>
    /// <returns>The outcome of the attempt.</returns>
    public AttemptResult Attempt(ClientKeys keys, Func<bool> isCorrect)
    {
        lock (_attemptLock)
        {
            var block = Check(keys, out var wait);
            if (block != Block.None)
            {
                return new AttemptResult(block, wait, false, default);
            }

            if (isCorrect())
            {
                RegisterSuccess(keys);
                return new AttemptResult(Block.None, TimeSpan.Zero, true, default);
            }

            return new AttemptResult(Block.None, TimeSpan.Zero, false, RegisterFailure(keys));
        }
    }

    /// <summary>
    /// Checks whether a login attempt from the client may go ahead (call before checking the PIN).
    /// </summary>
    /// <param name="keys">The client's throttling keys.</param>
    /// <param name="retryAfter">The suggested wait when blocked, otherwise zero.</param>
    /// <returns>Which limit blocks the attempt, or <see cref="Block.None"/>.</returns>
    public Block Check(ClientKeys keys, out TimeSpan retryAfter)
    {
        var clientLocked = Clients.IsLockedOut(keys.Narrow, out var narrowWait);
        var wideWait = TimeSpan.Zero;
        var prefixLocked = keys.Wide != null && Prefixes.IsLockedOut(keys.Wide, out wideWait);
        if (clientLocked || prefixLocked)
        {
            retryAfter = narrowWait > wideWait ? narrowWait : wideWait;
            return Block.Client;
        }

        return Global.IsThrottled(out retryAfter) ? Block.Global : Block.None;
    }

    /// <summary>
    /// Records a wrong PIN against the client's buckets and the global budget.
    /// </summary>
    /// <param name="keys">The client's throttling keys.</param>
    /// <returns>The combined result of the client's buckets.</returns>
    public LoginThrottle.FailureResult RegisterFailure(ClientKeys keys)
    {
        Global.RegisterFailure();

        var narrow = Clients.RegisterFailure(keys.Narrow);
        if (keys.Wide == null)
        {
            return narrow;
        }

        var wide = Prefixes.RegisterFailure(keys.Wide);
        var retryAfter = narrow.RetryAfter > wide.RetryAfter ? narrow.RetryAfter : wide.RetryAfter;
        return new LoginThrottle.FailureResult(
            narrow.LockedOut || wide.LockedOut,
            Math.Min(narrow.AttemptsLeft, wide.AttemptsLeft),
            retryAfter);
    }

    /// <summary>
    /// A correct PIN resets the client's own bucket. The /48 bucket and the global budget are left to
    /// age out, so a legitimate user cannot reset an attacker's count on a shared prefix.
    /// </summary>
    /// <param name="keys">The client's throttling keys.</param>
    public void RegisterSuccess(ClientKeys keys) => Clients.RegisterSuccess(keys.Narrow);

    public void Clear()
    {
        Clients.Clear();
        Prefixes.Clear();
        Global.Clear();
    }

    internal readonly struct AttemptResult
    {
        public AttemptResult(Block block, TimeSpan retryAfter, bool correct, LoginThrottle.FailureResult failure)
        {
            Block = block;
            RetryAfter = retryAfter;
            Correct = correct;
            Failure = failure;
        }

        /// <summary>
        /// Gets the limit that refused the attempt (the PIN was not checked), or <see cref="LoginGuard.Block.None"/>.
        /// </summary>
        public Block Block { get; }

        /// <summary>
        /// Gets the suggested wait when <see cref="Block"/> is set.
        /// </summary>
        public TimeSpan RetryAfter { get; }

        public bool Correct { get; }

        /// <summary>
        /// Gets the recorded failure for a wrong PIN.
        /// </summary>
        public LoginThrottle.FailureResult Failure { get; }
    }
}
