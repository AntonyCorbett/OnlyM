namespace OnlyM.Core.Services.Options;

public enum RemoteControlMode
{
    /// <summary>
    /// HTTP listener on the local network only.
    /// </summary>
    Local,

    /// <summary>
    /// HTTP listener plus a Cloudflare Quick Tunnel (public trycloudflare.com URL).
    /// </summary>
    Cloudflare,
}