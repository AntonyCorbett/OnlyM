using System;

namespace OnlyM.Services.RemoteControl;

public interface IRemoteControlService
{
    /// <summary>
    /// Raised (on any thread) when the listener, tunnel URL or tunnel error changes.
    /// </summary>
    event EventHandler? StateChanged;

    bool IsRunning { get; }

    string? ListenUrl { get; }

    /// <summary>
    /// Gets a value indicating whether the listener fell back to localhost (no URL reservation),
    /// so other devices cannot connect.
    /// </summary>
    bool IsLocalhostOnly { get; }

    /// <summary>
    /// Gets the port that could not be used because another program already listens on it
    /// (the listener is not running), or null.
    /// </summary>
    int? PortInUse { get; }

    /// <summary>
    /// Gets the public Cloudflare Quick Tunnel URL, once known (Cloudflare mode only).
    /// </summary>
    string? TunnelUrl { get; }

    /// <summary>
    /// Gets the last tunnel error (e.g. cloudflared not installed), or null.
    /// </summary>
    string? TunnelError { get; }

    /// <summary>
    /// Gets the Cloudflare tunnel state (Idle in Local mode).
    /// </summary>
    TunnelState TunnelState { get; }

    /// <summary>
    /// Stops the Cloudflare tunnel (if any) and starts it again using the current options.
    /// </summary>
    void RetryTunnel();

    void Start(string? prefix = null);

    void Stop();

    /// <summary>
    /// Starts or stops the listener and tunnel to match the current options.
    /// </summary>
    void ApplySettings();

    /// <summary>
    /// Stops and restarts the listener (and tunnel) using the current options, e.g. after a URL reservation was added.
    /// </summary>
    void Restart();
}