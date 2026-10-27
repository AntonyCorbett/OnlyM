using OnlyM.Core.Services.Options;
using OnlyM.Services.RemoteControl;

namespace OnlyM.Tests;

public sealed class RemoteSessionStoreTests
{
    private DateTime _now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void NewSessionIsValid()
    {
        var store = new RemoteSessionStore(() => _now);
        var token = store.Create();

        Assert.Equal(64, token.Length);
        Assert.True(store.TryValidate(token));
        Assert.False(store.TryValidate("nope"));
        Assert.False(store.TryValidate(null));
        Assert.False(store.TryValidate(string.Empty));
    }

    [Fact]
    public void IdleSessionExpires()
    {
        var store = new RemoteSessionStore(() => _now);
        var token = store.Create();

        _now = _now.AddMinutes(29);
        Assert.True(store.TryValidate(token));

        _now = _now.AddMinutes(30);
        Assert.False(store.TryValidate(token));
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void ActivitySlidesTheIdleTimeoutUntilTheAbsoluteLimit()
    {
        var store = new RemoteSessionStore(() => _now);
        var token = store.Create();

        // a request every 20 minutes keeps the session alive...
        for (var i = 0; i < 35; i++)
        {
            _now = _now.AddMinutes(20);
            Assert.True(store.TryValidate(token), $"request {i}");
        }

        // ...but not beyond 12 hours after login
        _now = _now.AddMinutes(20);
        Assert.False(store.TryValidate(token));
    }

    [Fact]
    public void ClearRemovesAllSessions()
    {
        var store = new RemoteSessionStore(() => _now);
        var a = store.Create();
        var b = store.Create();

        store.Clear();

        Assert.False(store.TryValidate(a));
        Assert.False(store.TryValidate(b));
    }

    [Fact]
    public void SessionCountIsBounded()
    {
        var store = new RemoteSessionStore(() => _now, maxSessions: 3);
        var first = store.Create();
        for (var i = 0; i < 5; i++)
        {
            _now = _now.AddSeconds(1);
            store.Create();
        }

        Assert.Equal(3, store.Count);
        Assert.False(store.TryValidate(first));
    }

    [Theory]
    [InlineData("1234", true, false)]
    [InlineData("123456", true, true)]
    [InlineData("12345678", true, true)]
    [InlineData("123", false, false)]
    [InlineData("123456789", false, false)]
    [InlineData("12a4", false, false)]
    [InlineData("", false, false)]
    [InlineData(null, false, false)]
    public void PinLengthRules(string? pin, bool valid, bool validForCloudflare)
    {
        Assert.Equal(valid, Options.IsValidRemoteControlPin(pin));
        Assert.Equal(validForCloudflare, Options.IsValidCloudflarePin(pin));
    }
}
