using System.Net;
using OnlyM.Services.RemoteControl;

namespace OnlyM.Tests;

public sealed class LoginGuardTests
{
    private DateTime _now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void IPv4KeyIsTheFullAddress()
    {
        var keys = ClientKeys.For(IPAddress.Parse("203.0.113.5"), "cf:");
        Assert.Equal("cf:203.0.113.5", keys.Narrow);
        Assert.Null(keys.Wide);
    }

    [Fact]
    public void IPv4MappedIPv6IsNormalisedToIPv4()
    {
        var mapped = ClientKeys.For(IPAddress.Parse("::ffff:203.0.113.5"));
        var plain = ClientKeys.For(IPAddress.Parse("203.0.113.5"));
        Assert.Equal(plain.Narrow, mapped.Narrow);
        Assert.Equal("203.0.113.5", mapped.Narrow);
        Assert.Null(mapped.Wide);
    }

    [Fact]
    public void UnknownAddressHasOneKey()
    {
        var keys = ClientKeys.For(null);
        Assert.Equal(ClientKeys.Unknown, keys.Narrow);
        Assert.Null(keys.Wide);
    }

    [Fact]
    public void IPv6IsKeyedBy64And48Prefixes()
    {
        var keys = ClientKeys.For(IPAddress.Parse("2a04:4e41:84:12ab:1111:2222:3333:4444"), "cf:");
        Assert.Equal("cf:2a04:4e41:84:12ab::/64", keys.Narrow);
        Assert.Equal("cf:2a04:4e41:84::/48", keys.Wide);
    }

    [Fact]
    public void RotatingInterfaceIdKeepsThe64Key()
    {
        var a = ClientKeys.For(IPAddress.Parse("2a04:4e41:84:7::1"));
        var b = ClientKeys.For(IPAddress.Parse("2a04:4e41:84:7:dead:beef:cafe:f00d"));
        var c = ClientKeys.For(IPAddress.Parse("fe80::1%12"));
        var d = ClientKeys.For(IPAddress.Parse("fe80::2%3"));
        Assert.Equal(a.Narrow, b.Narrow);
        Assert.Equal(c.Narrow, d.Narrow);
    }

    [Fact]
    public void Rotating4thHextetChanges64ButKeeps48()
    {
        var a = ClientKeys.For(IPAddress.Parse("2a04:4e41:84:1::1"));
        var b = ClientKeys.For(IPAddress.Parse("2a04:4e41:84:ffff::1"));
        Assert.NotEqual(a.Narrow, b.Narrow);
        Assert.Equal(a.Wide, b.Wide);
    }

    [Fact]
    public void RotatingInterfaceIdsWithinOne64LocksAfterFive()
    {
        var guard = new LoginGuard(() => _now);
        for (var i = 1; i <= 5; i++)
        {
            var keys = ClientKeys.For(IPAddress.Parse($"2a04:4e41:84:7:{i:x}::{i * 7919:x}"));
            Assert.Equal(LoginGuard.Block.None, guard.Check(keys, out _));
            var r = guard.RegisterFailure(keys);
            Assert.Equal(i == 5, r.LockedOut);
            Assert.Equal(5 - i, r.AttemptsLeft);
        }

        var other = ClientKeys.For(IPAddress.Parse("2a04:4e41:84:7:abcd::99"));
        Assert.Equal(LoginGuard.Block.Client, guard.Check(other, out var wait));
        Assert.Equal(TimeSpan.FromSeconds(60), wait);
    }

    [Fact]
    public void Rotating4thHextetAndInterfaceIdWithinOne48Locks()
    {
        // The reported bypass: a new 4th hextet and interface ID on every request
        var guard = new LoginGuard(() => _now);
        LoginThrottle.FailureResult r = default;
        for (var i = 1; i <= LoginGuard.Prefix48MaxFailures; i++)
        {
            var keys = ClientKeys.For(IPAddress.Parse($"2a04:4e41:84:{i * 4099:x}:{i:x}:{i * 31:x}::{i * 13:x}"), "cf:");
            Assert.Equal(LoginGuard.Block.None, guard.Check(keys, out _));
            r = guard.RegisterFailure(keys);
            if (i < LoginGuard.Prefix48MaxFailures)
            {
                Assert.False(r.LockedOut);
                Assert.Equal(Math.Min(4, LoginGuard.Prefix48MaxFailures - i), r.AttemptsLeft);
            }
        }

        Assert.True(r.LockedOut);
        Assert.Equal(TimeSpan.FromSeconds(60), r.RetryAfter);

        var sameSite = ClientKeys.For(IPAddress.Parse("2a04:4e41:84:beef::1"), "cf:");
        Assert.Equal(LoginGuard.Block.Client, guard.Check(sameSite, out _));

        // Another /48, an IPv4 client and a LAN client with the same /48 are unaffected
        Assert.Equal(LoginGuard.Block.None, guard.Check(ClientKeys.For(IPAddress.Parse("2a04:4e41:85:1::1"), "cf:"), out _));
        Assert.Equal(LoginGuard.Block.None, guard.Check(ClientKeys.For(IPAddress.Parse("203.0.113.5"), "cf:"), out _));
        Assert.Equal(LoginGuard.Block.None, guard.Check(ClientKeys.For(IPAddress.Parse("2a04:4e41:84:beef::1")), out _));

        _now = _now.AddSeconds(60);
        Assert.Equal(LoginGuard.Block.None, guard.Check(sameSite, out _));
    }

    [Fact]
    public void SuccessResetsOnlyTheClientBucket()
    {
        var guard = new LoginGuard(() => _now);
        var keys = ClientKeys.For(IPAddress.Parse("2a04:4e41:84:1::1"));
        for (var i = 0; i < 4; i++)
        {
            guard.RegisterFailure(keys);
        }

        guard.RegisterSuccess(keys);
        var r = guard.RegisterFailure(keys);
        Assert.False(r.LockedOut);
        Assert.Equal(4, r.AttemptsLeft); // /64 reset; /48 has 5 of 12 failures, 7 left
        Assert.Equal(5, guard.Global.FailureCount);
    }

    [Fact]
    public void IPv4ClientsAreIndependent()
    {
        var guard = new LoginGuard(() => _now);
        var a = ClientKeys.For(IPAddress.Parse("198.51.100.1"));
        var b = ClientKeys.For(IPAddress.Parse("198.51.100.2"));
        for (var i = 0; i < 5; i++)
        {
            guard.RegisterFailure(a);
        }

        Assert.Equal(LoginGuard.Block.Client, guard.Check(a, out _));
        Assert.Equal(LoginGuard.Block.None, guard.Check(b, out _));
    }

    [Fact]
    public void GlobalBackstopThrottlesProgressivelyThenDrains()
    {
        var guard = new LoginGuard(() => _now);
        var budget = guard.Global.Budget;
        Assert.Equal(20, budget);

        // Distributed attack: one wrong PIN from each of many addresses
        for (var i = 0; i < budget; i++)
        {
            var keys = ClientKeys.For(IPAddress.Parse($"198.51.100.{i + 1}"));
            Assert.Equal(LoginGuard.Block.None, guard.Check(keys, out _));
            guard.RegisterFailure(keys);
        }

        var fresh = ClientKeys.For(IPAddress.Parse("192.0.2.77"));
        var waits = new List<int>();
        for (var i = 0; i < 6; i++)
        {
            Assert.Equal(LoginGuard.Block.Global, guard.Check(fresh, out var wait));
            waits.Add((int)wait.TotalSeconds);
        }

        Assert.Equal(new[] { 5, 10, 20, 40, 60, 60 }, waits);

        // Retry-After never exceeds the time until the oldest failure ages out
        _now = _now.AddMinutes(10).AddSeconds(-2);
        Assert.Equal(LoginGuard.Block.Global, guard.Check(fresh, out var nearlyDrained));
        Assert.Equal(TimeSpan.FromSeconds(2), nearlyDrained);

        // Window drained: logins allowed again and the pressure resets
        _now = _now.AddSeconds(2);
        Assert.Equal(0, guard.Global.FailureCount);
        Assert.Equal(LoginGuard.Block.None, guard.Check(fresh, out _));
        for (var i = 0; i < budget; i++)
        {
            guard.RegisterFailure(ClientKeys.For(IPAddress.Parse($"203.0.113.{i + 1}")));
        }

        Assert.Equal(LoginGuard.Block.Global, guard.Check(fresh, out var again));
        Assert.Equal(TimeSpan.FromSeconds(5), again);
    }

    [Fact]
    public void GlobalBudgetIsRolling()
    {
        var backstop = new GlobalLoginBackstop(() => _now, budget: 3, window: TimeSpan.FromMinutes(10));
        backstop.RegisterFailure();
        _now = _now.AddMinutes(4);
        backstop.RegisterFailure();
        backstop.RegisterFailure();
        Assert.True(backstop.IsThrottled(out _));

        // Only the first failure has aged out: one more attempt is allowed
        _now = _now.AddMinutes(6);
        Assert.False(backstop.IsThrottled(out _));
        backstop.RegisterFailure();
        Assert.True(backstop.IsThrottled(out var wait));
        Assert.Equal(TimeSpan.FromSeconds(5), wait);
    }

    [Fact]
    public void SuccessfulLoginsDoNotUseTheGlobalBudget()
    {
        var guard = new LoginGuard(() => _now);
        var keys = ClientKeys.For(IPAddress.Parse("198.51.100.1"));
        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(LoginGuard.Block.None, guard.Check(keys, out _));
            guard.RegisterSuccess(keys);
        }

        Assert.Equal(0, guard.Global.FailureCount);
    }

    [Fact]
    public void ExistingSessionsAreUnaffectedByLoginThrottling()
    {
        // Logged-in requests are authorised by the session store alone; the guard only gates /api/login
        var guard = new LoginGuard(() => _now);
        var sessions = new RemoteSessionStore(() => _now);
        var token = sessions.Create();
        for (var i = 0; i < guard.Global.Budget; i++)
        {
            guard.RegisterFailure(ClientKeys.For(IPAddress.Parse($"198.51.100.{i + 1}")));
        }

        Assert.Equal(LoginGuard.Block.Global, guard.Check(ClientKeys.For(IPAddress.Parse("192.0.2.1")), out _));
        Assert.True(sessions.TryValidate(token));
    }

    [Fact]
    public void ClearResetsEverything()
    {
        var guard = new LoginGuard(() => _now);
        for (var i = 0; i < guard.Global.Budget; i++)
        {
            guard.RegisterFailure(ClientKeys.For(IPAddress.Parse("2a04:4e41:84:1::1")));
        }

        guard.Clear();
        Assert.Equal(LoginGuard.Block.None, guard.Check(ClientKeys.For(IPAddress.Parse("2a04:4e41:84:1::1")), out _));
        Assert.Equal(0, guard.Global.FailureCount);
    }
}
