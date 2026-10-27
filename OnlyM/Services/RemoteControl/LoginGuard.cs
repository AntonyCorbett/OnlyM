using System;

namespace OnlyM.Services.RemoteControl;

/// <summary>
/// All login rate limits together: per client (full IPv4 address or IPv6 /64, 5 failures), per IPv6 /48
/// (12 failures), each with a 60 s lockout, plus the <see cref="GlobalLoginBackstop"/>. Thread-safe.
/// </summary>
internal sealed class LoginGuard
{
    public const int Prefix48MaxFailures = 12;

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
}
