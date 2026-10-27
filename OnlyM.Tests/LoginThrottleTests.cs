using OnlyM.Services.RemoteControl;

namespace OnlyM.Tests;

public sealed class LoginThrottleTests
{
    private DateTime _now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void FiveFailuresLockOutForSixtySeconds()
    {
        var throttle = new LoginThrottle(() => _now);

        for (var i = 1; i <= 4; i++)
        {
            var r = throttle.RegisterFailure("10.0.0.1");
            Assert.False(r.LockedOut);
            Assert.Equal(5 - i, r.AttemptsLeft);
        }

        var last = throttle.RegisterFailure("10.0.0.1");
        Assert.True(last.LockedOut);
        Assert.Equal(TimeSpan.FromSeconds(60), last.RetryAfter);

        _now = _now.AddSeconds(59);
        Assert.True(throttle.IsLockedOut("10.0.0.1", out var wait));
        Assert.Equal(TimeSpan.FromSeconds(1), wait);

        _now = _now.AddSeconds(1);
        Assert.False(throttle.IsLockedOut("10.0.0.1", out _));
        Assert.Equal(4, throttle.RegisterFailure("10.0.0.1").AttemptsLeft);
    }

    [Fact]
    public void LockoutIsPerClient()
    {
        var throttle = new LoginThrottle(() => _now);
        for (var i = 0; i < 5; i++)
        {
            throttle.RegisterFailure("10.0.0.1");
        }

        Assert.True(throttle.IsLockedOut("10.0.0.1", out _));
        Assert.False(throttle.IsLockedOut("10.0.0.2", out _));
        Assert.False(throttle.RegisterFailure("10.0.0.2").LockedOut);
    }

    [Fact]
    public void FailureWhileLockedDoesNotExtendLockout()
    {
        var throttle = new LoginThrottle(() => _now);
        for (var i = 0; i < 5; i++)
        {
            throttle.RegisterFailure("a");
        }

        _now = _now.AddSeconds(30);
        var r = throttle.RegisterFailure("a");
        Assert.True(r.LockedOut);
        Assert.Equal(TimeSpan.FromSeconds(30), r.RetryAfter);
    }

    [Fact]
    public void SuccessResetsFailures()
    {
        var throttle = new LoginThrottle(() => _now);
        throttle.RegisterFailure("a");
        throttle.RegisterFailure("a");
        throttle.RegisterSuccess("a");

        Assert.Equal(0, throttle.Count);
        Assert.Equal(4, throttle.RegisterFailure("a").AttemptsLeft);
    }

    [Fact]
    public void OldFailuresAreForgotten()
    {
        var throttle = new LoginThrottle(() => _now, failureWindow: TimeSpan.FromMinutes(15));
        for (var i = 0; i < 4; i++)
        {
            throttle.RegisterFailure("a");
        }

        _now = _now.AddMinutes(16);
        Assert.Equal(4, throttle.RegisterFailure("a").AttemptsLeft);
    }

    [Fact]
    public void StaleEntriesArePruned()
    {
        var throttle = new LoginThrottle(() => _now, failureWindow: TimeSpan.FromMinutes(15));
        for (var i = 0; i < 50; i++)
        {
            throttle.RegisterFailure($"10.0.0.{i}");
        }

        Assert.Equal(50, throttle.Count);

        _now = _now.AddMinutes(20);
        throttle.RegisterFailure("10.0.1.1");
        Assert.Equal(1, throttle.Count);
    }

    [Fact]
    public void SizeIsBoundedAndLockedClientsAreKept()
    {
        var throttle = new LoginThrottle(() => _now, maxEntries: 10);
        for (var i = 0; i < 5; i++)
        {
            throttle.RegisterFailure("attacker");
        }

        for (var i = 0; i < 100; i++)
        {
            _now = _now.AddMilliseconds(10);
            throttle.RegisterFailure($"client{i}");
        }

        Assert.True(throttle.Count <= 10);
        Assert.True(throttle.IsLockedOut("attacker", out _));
    }
}
