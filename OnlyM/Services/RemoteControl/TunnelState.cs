namespace OnlyM.Services.RemoteControl;

/// <summary>
/// State of the Cloudflare Quick Tunnel process.
/// </summary>
public enum TunnelState
{
    Idle,
    Starting,
    WaitingForUrl,
    Connected,
    Failed,
}