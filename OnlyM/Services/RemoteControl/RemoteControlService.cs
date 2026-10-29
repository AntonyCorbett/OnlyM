using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OnlyM.Core.Services.Media;
using OnlyM.Core.Services.Options;
using OnlyM.Core.Utils;
using OnlyM.Models;
using OnlyM.ViewModel;
using Serilog;

namespace OnlyM.Services.RemoteControl;

internal sealed class RemoteControlService : IRemoteControlService, IDisposable
{
    private const string SessionCookieName = "onlym_session";
    private const string CfConnectingIpHeader = "CF-Connecting-IP";

    // Win32 errors from HttpListener.Start when another program already uses the port:
    // ERROR_SHARING_VIOLATION (a non-http.sys listener) and ERROR_ALREADY_EXISTS (another http.sys registration).
    private const int ErrorSharingViolation = 32;
    private const int ErrorAlreadyExists = 183;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly CloudflareTunnel _tunnel = new();
    private readonly RemoteSessionStore _sessions = new();
    private readonly LoginGuard _loginGuard = new();

    // Serialises Start/Stop/Restart/ApplySettings/RetryTunnel (reentrant, so they can call each other)
    private readonly object _lifecycleLock = new();
    private readonly OperatorViewModel _operatorViewModel;
    private readonly IThumbnailService _thumbnailService;
    private readonly IOptionsService _optionsService;
    private HttpListener? _listener;
    private CancellationTokenSource? _listenCts;
    private volatile bool _running;
    private volatile bool _tunnelBlockedByPin;

    public RemoteControlService(
        OperatorViewModel operatorViewModel,
        IThumbnailService thumbnailService,
        IOptionsService optionsService)
    {
        _operatorViewModel = operatorViewModel;
        _thumbnailService = thumbnailService;
        _optionsService = optionsService;
        _tunnel.StateChanged += (_, _) => StateChanged?.Invoke(this, EventArgs.Empty);
        _optionsService.RemoteControlChangedEvent += (_, _) => ApplySettings();
        _optionsService.RemoteControlPinChangedEvent += (_, _) =>
        {
            // PIN changed: everyone must log in again
            _sessions.Clear();
            _loginGuard.Clear();
            Log.Logger.Information("Remote control PIN changed; remote sessions cleared");

            if (_optionsService.RemoteControlEnabled &&
                _optionsService.RemoteControlMode == RemoteControlMode.Cloudflare &&
                _tunnelBlockedByPin)
            {
                // e.g. the tunnel was blocked by a too-short PIN; try again with the new one
                Task.Run(RetryTunnel);
            }
        };
    }

    public event EventHandler? StateChanged;

    public bool IsRunning => _running;

    public string? ListenUrl { get; private set; }

    public bool IsLocalhostOnly =>
        _running && ListenUrl != null && ListenUrl.Contains("://localhost:", StringComparison.OrdinalIgnoreCase);

    public int? PortInUse { get; private set; }

    public void Restart()
    {
        lock (_lifecycleLock)
        {
            Log.Logger.Information("Restarting remote control listener");
            if (_running || _tunnel.IsRunning)
            {
                Stop();
            }

            ApplySettings();
        }
    }

    public string? TunnelUrl => _tunnel.PublicUrl;

    public string? TunnelError => _tunnel.Error;

    public TunnelState TunnelState => _tunnel.State;

    public void RetryTunnel()
    {
        lock (_lifecycleLock)
        {
            Log.Logger.Information("Retrying Cloudflare tunnel");
            _tunnel.Stop();
            ApplySettings();
        }
    }

    public void ApplySettings()
    {
        try
        {
            lock (_lifecycleLock)
            {
                ApplySettingsCore();
            }
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Failed to apply remote control settings. A URL reservation (netsh http add urlacl) may be needed");
        }
        finally
        {
            // raised outside the lock so handlers can't deadlock against it
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        Stop();
        _tunnel.Dispose();
    }

    public void Start(string? prefix = null)
    {
        lock (_lifecycleLock)
        {
            StartCore(prefix);
        }
    }

    public void Stop()
    {
        lock (_lifecycleLock)
        {
            _tunnel.Stop();
            _running = false;
            PortInUse = null;

            var listener = _listener;
            var cts = _listenCts;
            _listener = null;
            _listenCts = null;

            // Cancel first so the accept loop treats the listener shutdown as expected
            cts?.Cancel();
            if (listener != null)
            {
                try
                {
                    listener.Stop();
                }
                catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException)
                {
                    // ignore
                }

                listener.Close();
            }

            cts?.Dispose();
            Log.Logger.Information("Remote control stopped");
        }
    }

    /// <summary>
    /// Gets a value indicating whether the remote may show and change the volume. Both "Allow volume control from the
    /// remote" and the operator's "Show volume control in toolbar" must be on, so the remote never sets a volume that
    /// the operator has no control to see or undo.
    /// </summary>
    /// <param name="optionsService">The options service.</param>
    /// <returns>True if the remote volume control is allowed.</returns>
    internal static bool IsRemoteVolumeEnabled(IOptionsService optionsService)
    {
        return optionsService.ShowRemoteVolumeControl && optionsService.ShowVolumeControl;
    }

    /// <summary>
    /// Builds the /api/status reply. The slide fields are null unless the active item is a slideshow
    /// (currentSlideIndex is 0-based). canSeek follows the operator's position slider
    /// (<see cref="MediaItem.IsSliderVisible"/>): audio/video, scrubbing allowed in Settings, and paused.
    /// </summary>
    /// <param name="active">The active media item, or null if nothing is playing.</param>
    /// <param name="volume">The current volume.</param>
    /// <param name="volumeEnabled">Whether the remote shows the volume control.</param>
    /// <returns>The reply object, serialised as JSON.</returns>
    internal static object BuildStatus(MediaItem? active, int volume, bool volumeEnabled)
    {
        var isSlideshow = active?.IsSlideshow == true;
        return new
        {
            isPlaying = active != null && !active.IsPaused,
            isPaused = active?.IsPaused ?? false,
            currentItemId = active?.Id.ToString(),
            positionSeconds = (active?.PlaybackPositionDeciseconds ?? 0) / 10.0,
            durationSeconds = (active?.DurationDeciseconds ?? 0) / 10.0,
            canSeek = active?.IsSliderVisible == true,
            volume,
            volumeEnabled,
            currentSlideIndex = isSlideshow ? active!.CurrentSlideshowIndex : (int?)null,
            slideCount = isSlideshow ? active!.SlideshowCount : (int?)null,
            isRolling = isSlideshow ? active!.IsRollingSlideshow : (bool?)null,
        };
    }

    /// <summary>
    /// Gets a value indicating whether Next/Previous should change slide rather than media item: the active item
    /// is a slideshow that can still move in that direction (the same rule as the operator's slide buttons).
    /// </summary>
    /// <param name="active">The active media item, or null if nothing is playing.</param>
    /// <param name="step">Positive for Next, negative for Previous.</param>
    /// <returns>True to change slide, false to change media item.</returns>
    internal static bool ShouldChangeSlide(MediaItem? active, int step)
    {
        if (active is not { IsSlideshow: true })
        {
            return false;
        }

        return step > 0 ? active.IsNextSlideButtonEnabled : active.IsPreviousSlideButtonEnabled;
    }

    /// <summary>
    /// Builds one /api/media entry. Slideshows also get slideCount and the operator card's localised
    /// "(contains n slides)" text as subtitle (both null for other media, and subtitle null until the slides are counted).
    /// </summary>
    /// <param name="m">The media item.</param>
    /// <returns>The entry object, serialised as JSON.</returns>
    internal static object BuildMediaEntry(MediaItem m)
    {
        var slideCount = m.IsSlideshow ? m.SlideshowCount : (int?)null;
        return new
        {
            id = m.Id.ToString(),
            name = m.Title ?? Path.GetFileNameWithoutExtension(m.FilePath ?? string.Empty),
            type = m.MediaType?.Classification.ToString() ?? "Unknown",
            durationSeconds = m.DurationDeciseconds / 10.0,
            isActive = m.IsMediaActive,
            isPaused = m.IsPaused,
            hasThumbnail = m.FilePath != null,
            slideCount,
            subtitle = slideCount > 0 ? MediaItem.GetSlideCountText(slideCount.Value, m.IsRollingSlideshow) : null,
        };
    }

    /// <summary>
    /// Decides whether /api/seek may move <paramref name="active"/> to <paramref name="secondsText"/>, using the
    /// operator's rule for showing the position slider (<see cref="MediaItem.IsSliderVisible"/>).
    /// </summary>
    /// <param name="active">The active media item, or null when nothing is playing.</param>
    /// <param name="secondsText">The requested position in seconds (invariant culture, e.g. "12.5").</param>
    /// <returns>
    /// 200 with the position in deciseconds (clamped to the item's duration), 400 for a malformed or negative
    /// value, or 409 with the reason when the item can't be scrubbed now (e.g. it is playing).
    /// </returns>
    internal static (int StatusCode, string? Error, int Deciseconds) PlanSeek(MediaItem? active, string secondsText)
    {
        // No sign or exponent: "-5", "1e3", "NaN" and "Infinity" are all rejected.
        if (!double.TryParse(secondsText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var seconds) ||
            !double.IsFinite(seconds))
        {
            return (400, Properties.Resources.REMOTE_CONTROL_API_BAD_POSITION, 0);
        }

        if (active == null)
        {
            return (409, Properties.Resources.REMOTE_CONTROL_API_NOTHING_PLAYING, 0);
        }

        if (!active.HasDuration)
        {
            return (409, Properties.Resources.REMOTE_CONTROL_API_CANT_SCRUB, 0);
        }

        if (!active.AllowPositionSeeking)
        {
            return (409, Properties.Resources.REMOTE_CONTROL_API_SEEKING_OFF, 0);
        }

        if (!active.IsSliderVisible)
        {
            return (409, Properties.Resources.REMOTE_CONTROL_API_PAUSE_TO_SCRUB, 0);
        }

        var max = Math.Max(0, active.DurationDeciseconds);
        var deciseconds = (int)Math.Min(max, Math.Round(seconds * 10, MidpointRounding.AwayFromZero));
        return (200, null, deciseconds);
    }

    internal static bool IsAddressInUse(int errorCode) => errorCode is ErrorSharingViolation or ErrorAlreadyExists;

    internal static string SerializeJson(object data) => JsonSerializer.Serialize(data, JsonOptions);

    private void StartCore(string? prefix)
    {
        if (_running)
        {
            return;
        }

        // Listen on all interfaces so devices on the LAN can reach the remote.
        // Requires a one-time URL reservation (as admin): netsh http add urlacl url=http://+:<port>/ user=Everyone
        var port = _optionsService.RemoteControlPort;
        prefix ??= $"http://+:{port}/";
        PortInUse = null;
        HttpListener listener;
        try
        {
            try
            {
                listener = CreateAndStart(prefix);
            }
            catch (HttpListenerException ex) when (ex.ErrorCode == 5 && !prefix.Contains("://localhost:", StringComparison.OrdinalIgnoreCase))
            {
                // Access denied: listening on anything but localhost needs a URL reservation
                // (netsh http add urlacl url=<prefix> user=Everyone). Fall back so the remote still works locally.
                var fallback = $"http://localhost:{port}/";
                Log.Logger.Warning(
                    "Remote control could not listen on {Prefix} (access denied; run 'netsh http add urlacl url={Prefix} user=Everyone' as admin). Falling back to {Fallback}",
                    prefix,
                    prefix,
                    fallback);
                prefix = fallback;
                listener = CreateAndStart(prefix);
            }
        }
        catch (HttpListenerException ex) when (IsAddressInUse(ex.ErrorCode))
        {
            // Another program (or another OnlyM) already uses the port; Settings asks the user to choose another one
            PortInUse = port;
            ListenUrl = null;
            Log.Logger.Warning(ex, "Remote control could not listen on {Prefix}: port {Port} is already in use", prefix, port);
            return;
        }

        // Each accept loop owns its listener and token, so a loop from a previous Start can never touch the new listener
        var cts = new CancellationTokenSource();
        _listener = listener;
        _listenCts = cts;
        ListenUrl = prefix;
        _running = true;

        Log.Logger.Information("Remote control listening on {Url}", prefix);
        _ = Task.Run(() => ListenLoop(listener, cts.Token));
    }

    private void ApplySettingsCore()
    {
        if (!_optionsService.RemoteControlEnabled)
        {
            if (_running || _tunnel.IsRunning)
            {
                Stop();
            }

            return;
        }

        if (!_running)
        {
            Start();
        }

        _tunnelBlockedByPin = false;
        if (!_running)
        {
            // e.g. the port is in use (see PortInUse); without a listener there is nothing to tunnel to
            _tunnel.Stop();
            return;
        }

        if (_optionsService.RemoteControlMode != RemoteControlMode.Cloudflare)
        {
            _tunnel.Stop();
            return;
        }

        if (!EnsureCloudflarePin())
        {
            _tunnelBlockedByPin = true;
            _tunnel.Stop();
            _tunnel.SetFailed(Properties.Resources.REMOTE_CONTROL_TUNNEL_PIN_TOO_SHORT);
            return;
        }

        var port = _optionsService.RemoteControlPort;
        if (_tunnel.BeginStart())
        {
            // cloudflared can take a while to start; don't block the caller (often the UI thread)
            _ = Task.Run(() => _tunnel.Start(port));
        }
    }

    /// <summary>
    /// The tunnel makes the remote reachable from the internet, so a PIN of at least
    /// <see cref="Options.MinCloudflarePinLength"/> digits is mandatory. Generates one if none is set.
    /// </summary>
    /// <returns>False if the existing PIN is too short (the tunnel must not start).</returns>
    private bool EnsureCloudflarePin()
    {
        var pin = _optionsService.RemoteControlPin;
        if (string.IsNullOrEmpty(pin))
        {
            _optionsService.RemoteControlPin = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
            Log.Logger.Information("Cloudflare mode requires a remote control PIN; a random 6-digit PIN was generated (see Settings)");
            return true;
        }

        if (!Options.IsValidCloudflarePin(pin))
        {
            Log.Logger.Warning(
                "Cloudflare tunnel not started: the remote control PIN must be at least {Length} digits",
                Options.MinCloudflarePinLength);
            return false;
        }

        return true;
    }

    private bool IsAuthorised(HttpListenerRequest req)
    {
        if (string.IsNullOrEmpty(_optionsService.RemoteControlPin))
        {
            return true;
        }

        // Validating also slides the session's idle timeout; expired sessions get 401 and the page shows the PIN prompt
        return _sessions.TryValidate(req.Cookies[SessionCookieName]?.Value);
    }

    /// <summary>
    /// Identifies the client for login throttling. Requests relayed by cloudflared arrive from loopback,
    /// so for those the real client address comes from Cloudflare's CF-Connecting-IP header. The header is
    /// trusted only for loopback requests while the tunnel is running (and, if the browser sent an Origin,
    /// only when it is the tunnel's), so a LAN client cannot spoof it to dodge the limit. The address is then
    /// keyed by <see cref="ClientKeys"/> (full IPv4 address; IPv6 /64 and /48 prefixes).
    /// </summary>
    private ClientKeys GetClientKeys(HttpListenerRequest req)
    {
        var remote = req.RemoteEndPoint?.Address;
        if (remote != null &&
            IPAddress.IsLoopback(remote) &&
            IsFromTunnel(req) &&
            req.Headers[CfConnectingIpHeader] is { } header &&
            IPAddress.TryParse(header.Trim(), out var clientIp))
        {
            return ClientKeys.For(clientIp, "cf:");
        }

        return ClientKeys.For(remote);
    }

    private bool IsFromTunnel(HttpListenerRequest req)
    {
        if (_optionsService.RemoteControlMode != RemoteControlMode.Cloudflare || !_tunnel.IsRunning)
        {
            return false;
        }

        var origin = req.Headers["Origin"];
        if (string.IsNullOrEmpty(origin))
        {
            return true;
        }

        return TunnelAuthority() is { } tunnel &&
               Uri.TryCreate(origin, UriKind.Absolute, out var originUri) &&
               originUri.Authority.Equals(tunnel, StringComparison.OrdinalIgnoreCase);
    }

    private async Task Login(HttpListenerRequest req, HttpListenerResponse res)
    {
        string body;
        using (var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8))
        {
            body = await reader.ReadToEndAsync();
        }

        var pin = ParsePin(body, req.ContentType);
        var expected = _optionsService.RemoteControlPin;
        var client = GetClientKeys(req);

        var block = _loginGuard.Check(client, out var wait);
        if (block != LoginGuard.Block.None)
        {
            if (block == LoginGuard.Block.Global)
            {
                Log.Logger.Warning(
                    "Remote control: global wrong-PIN budget used up ({Count} in {Minutes} minutes); login from {Client} refused, retry in {Seconds} s",
                    _loginGuard.Global.Budget,
                    _loginGuard.Global.Window.TotalMinutes,
                    client,
                    Math.Ceiling(wait.TotalSeconds));
            }

            await Locked(res, wait);
            return;
        }

        var correct = string.IsNullOrEmpty(expected) ||
                      (pin != null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(pin), Encoding.UTF8.GetBytes(expected)));
        if (!correct)
        {
            var result = _loginGuard.RegisterFailure(client);
            if (result.LockedOut)
            {
                Log.Logger.Warning(
                    "Remote control: too many wrong PIN attempts from {Client}; logins from it blocked for {Seconds} seconds",
                    client,
                    Math.Ceiling(result.RetryAfter.TotalSeconds));
                await Locked(res, result.RetryAfter);
                return;
            }

            res.StatusCode = 401;
            await Ok(res, new { error = "wrong", attemptsLeft = result.AttemptsLeft });
            return;
        }

        _loginGuard.RegisterSuccess(client);
        var token = _sessions.Create();
        res.Headers.Add("Set-Cookie", $"{SessionCookieName}={token}; Path=/; HttpOnly; SameSite=Strict");
        Log.Logger.Information("Remote control login from {Client}", client);
        await Ok(res);
    }

    private static async Task Locked(HttpListenerResponse res, TimeSpan wait)
    {
        var retryAfter = Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds));
        res.StatusCode = 429;
        res.Headers.Add("Retry-After", retryAfter.ToString(System.Globalization.CultureInfo.InvariantCulture));
        await Ok(res, new { error = "locked", retryAfter });
    }

    private static string? ParsePin(string body, string? contentType)
    {
        try
        {
            if (contentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true || body.TrimStart().StartsWith('{'))
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("pin", out var p))
                {
                    return p.ValueKind == JsonValueKind.Number ? p.GetRawText() : p.GetString();
                }

                return null;
            }

            foreach (var pair in body.Split('&'))
            {
                var kv = pair.Split('=', 2);
                if (kv.Length == 2 && kv[0] == "pin")
                {
                    return WebUtility.UrlDecode(kv[1]);
                }
            }
        }
        catch (JsonException)
        {
            // fall through
        }

        return null;
    }

    /// <summary>
    /// Only accept Host names that can legitimately point at this PC: IP addresses, localhost,
    /// the machine name (optionally with .local or the DNS domain) and the Cloudflare tunnel host.
    /// Stops DNS-rebinding pages (evil.example resolving to this PC) from passing the Origin check.
    /// </summary>
    private bool IsAllowedHost(HttpListenerRequest req)
    {
        var host = req.Url?.Host;
        if (string.IsNullOrEmpty(host))
        {
            return false;
        }

        host = host.Trim('[', ']').TrimEnd('.');
        if (IPAddress.TryParse(host, out _) || host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var machine = Environment.MachineName;
        if (host.Equals(machine, StringComparison.OrdinalIgnoreCase) ||
            host.Equals(machine + ".local", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var domain = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().DomainName;
        if (!string.IsNullOrEmpty(domain) && host.Equals($"{machine}.{domain}", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return TunnelAuthority() is { } tunnel &&
               host.Equals(new Uri("https://" + tunnel).Host, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Browsers send Origin on cross-site requests (and on same-origin POSTs). If present it must match
    /// the Host the request was sent to, or the public tunnel URL (cloudflared rewrites Host to localhost).
    /// Requests without Origin (curl, scripts, plain navigation) are allowed; the PIN still applies.
    /// </summary>
    private bool IsSameOrigin(HttpListenerRequest req)
    {
        var origin = req.Headers["Origin"];
        if (string.IsNullOrEmpty(origin))
        {
            return true;
        }

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var originUri))
        {
            return false; // includes "null" (sandboxed iframes, file:// pages)
        }

        var originAuthority = originUri.Authority;
        if (Uri.TryCreate("http://" + req.UserHostName, UriKind.Absolute, out var hostUri) &&
            originUri.Scheme == Uri.UriSchemeHttp &&
            originAuthority.Equals(hostUri.Authority, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return originUri.Scheme == Uri.UriSchemeHttps &&
               TunnelAuthority() is { } tunnel &&
               originAuthority.Equals(tunnel, StringComparison.OrdinalIgnoreCase);
    }

    private string? TunnelAuthority() =>
        _tunnel.PublicUrl is { } url && Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Authority : null;

    private static HttpListener CreateAndStart(string prefix)
    {
        var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        try
        {
            listener.Start();
            return listener;
        }
        catch
        {
            listener.Close();
            throw;
        }
    }

    private async Task ListenLoop(HttpListener listener, CancellationToken token)
    {
        while (!token.IsCancellationRequested && listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch (Exception ex) when (token.IsCancellationRequested || !listener.IsListening)
            {
                // expected when the listener is stopped (Stop/Restart)
                Log.Logger.Debug("Remote control accept loop ending ({Error})", ex.GetType().Name);
                break;
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Accept error");
                await Task.Delay(100, CancellationToken.None); // avoid a hot loop on a persistent error
                continue;
            }

            _ = Task.Run(() => HandleRequest(context), CancellationToken.None);
        }
    }

    private async Task HandleRequest(HttpListenerContext context)
    {
        var req = context.Request;
        var res = context.Response;

        // No CORS headers: the remote page is served from this listener, so all its calls are same-origin.
        try
        {
            var path = req.Url?.AbsolutePath.TrimEnd('/') ?? string.Empty;

            // Serve HTML remote control page at root
            if ((path.Length == 0 || path == "/" || path == "/remote") && req.HttpMethod == "GET")
            {
                await ServeHtmlPage(res);
                return;
            }

            if (!path.StartsWith("/api/", StringComparison.Ordinal))
            {
                // Non-API paths (favicon.ico, apple-touch-icon etc.) need no PIN; the page has no other static assets.
                res.StatusCode = path == "/favicon.ico" ? 204 : 404;
                return;
            }

            res.ContentType = "application/json; charset=utf-8";

            if (!IsAllowedHost(req) || !IsSameOrigin(req))
            {
                // Blocks other web pages (cross-site POSTs, DNS rebinding) from driving the API through a browser.
                Log.Logger.Warning(
                    "Remote control: rejected {Method} {Path} from {Address} (Host {Host}, Origin {Origin})",
                    req.HttpMethod,
                    path,
                    req.RemoteEndPoint?.Address,
                    req.UserHostName,
                    req.Headers["Origin"]);
                res.StatusCode = 403;
                await Ok(res, new { error = Properties.Resources.REMOTE_CONTROL_API_FORBIDDEN });
                return;
            }

            if (path == "/api/login" && req.HttpMethod == "POST")
            {
                await Login(req, res);
                return;
            }

            if (!IsAuthorised(req))
            {
                res.StatusCode = 401;
                await Ok(res, new { error = Properties.Resources.REMOTE_CONTROL_API_PIN_REQUIRED });
                return;
            }

            switch (path)
            {
                case "/api/media" when req.HttpMethod == "GET":
                    await ListMedia(res);
                    break;
                case "/api/status" when req.HttpMethod == "GET":
                    await Status(res);
                    break;
                case "/api/stop" when req.HttpMethod == "POST":
                    await StopCurrent(res);
                    break;
                case "/api/pause" when req.HttpMethod == "POST":
                    await TogglePause(res);
                    break;
                case "/api/next" when req.HttpMethod == "POST":
                    await PlayNext(res);
                    break;
                case "/api/previous" when req.HttpMethod == "POST":
                    await PlayPrevious(res);
                    break;
                default:
                    if (path.StartsWith("/api/play/", StringComparison.Ordinal) && req.HttpMethod == "POST")
                    {
                        var idStr = path["/api/play/".Length..];
                        await PlayById(idStr, res);
                    }
                    else if (path.StartsWith("/api/seek/", StringComparison.Ordinal) && req.HttpMethod == "POST")
                    {
                        await Seek(path["/api/seek/".Length..], res);
                    }
                    else if ((path.StartsWith("/api/volume/", StringComparison.Ordinal) || path == "/api/mute") && req.HttpMethod == "POST")
                    {
                        if (!IsRemoteVolumeEnabled(_optionsService))
                        {
                            // The remote volume control (or the operator's volume control) is turned off in Settings
                            res.StatusCode = 403;
                            await Ok(res, new { error = Properties.Resources.REMOTE_CONTROL_API_VOLUME_DISABLED });
                        }
                        else if (path == "/api/mute")
                        {
                            await ToggleMute(res);
                        }
                        else
                        {
                            await SetVolume(path["/api/volume/".Length..], res);
                        }
                    }
                    else if (path.StartsWith("/api/thumbnails/", StringComparison.Ordinal) && req.HttpMethod == "GET")
                    {
                        var idStr = path["/api/thumbnails/".Length..];
                        await ServeThumbnail(idStr, res);
                    }
                    else
                    {
                        res.StatusCode = 404;
                        await Ok(res, new { error = Properties.Resources.REMOTE_CONTROL_API_NOT_FOUND });
                    }

                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Request failed");
            res.StatusCode = 500;
            await Ok(res, new { error = ex.Message });
        }
        finally
        {
            res.Close();
        }
    }

    private static async Task ServeHtmlPage(HttpListenerResponse res)
    {
        res.ContentType = "text/html; charset=utf-8";
        res.Headers.Add("Cache-Control", "no-store"); // avoid stale copies of the page (e.g. through the tunnel)
        var html = """
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>OnlyM Remote</title>
<style>
  body { font-family: system-ui, sans-serif; margin: 0; padding: 12px; background: #111; color: #eee; }
  h1 { font-size: 1.4rem; margin: 0 0 12px; }
  .controls { display: flex; gap: 8px; margin-bottom: 16px; flex-wrap: wrap; }
  button { min-width: 56px; padding: 10px 16px; font-size: 1.5rem; line-height: 1; border: none; border-radius: 6px; background: #333; color: #fff; cursor: pointer; }
  button:active { background: #555; }
  button.primary { background: #0a7; }
  .media-grid { display: flex; flex-direction: column; gap: 8px; }
  .media-item { display: flex; align-items: center; gap: 10px; background: #222; border-radius: 8px; overflow: hidden; cursor: pointer; border: 2px solid transparent; }
  .media-item.active { border-color: #0a7; }
  .media-item img { flex: 0 0 auto; width: 120px; height: 68px; object-fit: cover; background: #000; }
  .media-item .text { flex: 1 1 auto; min-width: 0; padding: 6px 8px; }
  .media-item .name { font-size: 0.95rem; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
  .media-item .sub { font-size: 0.8rem; color: #aaa; margin-top: 2px; }
  button:disabled { opacity: 0.4; cursor: default; }
  .controls { position: relative; }
  .controls > button { flex: 1 1 0; min-width: 44px; max-width: 96px; padding-left: 0; padding-right: 0; }
  .volpop { position: absolute; right: 0; top: 100%; margin-top: 6px; z-index: 6; display: flex; align-items: center; gap: 10px; width: min(340px, calc(100vw - 24px)); box-sizing: border-box; padding: 10px 12px; background: #222; border: 1px solid #444; border-radius: 8px; box-shadow: 0 4px 16px rgba(0,0,0,0.6); }
  .volpop input { flex: 1 1 auto; min-width: 0; accent-color: #0a7; height: 28px; }
  .volpop span { min-width: 3em; text-align: right; }
  .volpop button { min-width: 0; padding: 8px 10px; font-size: 0.95rem; }
  .now { display: flex; align-items: center; gap: 12px; margin-bottom: 12px; padding: 10px; background: #222; border-radius: 8px; }
  .now img { width: 96px; height: 54px; object-fit: cover; background: #000; border-radius: 4px; }
  .now .info { flex: 1 1 auto; min-width: 0; }
  .now .label { font-size: 0.75rem; color: #9a9; text-transform: uppercase; letter-spacing: 0.05em; }
  .now .title { font-size: 1.05rem; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
  .now .bar { height: 4px; background: #444; border-radius: 2px; margin-top: 6px; overflow: hidden; }
  .now .bar div { height: 100%; width: 0; background: #0a7; }
  .now .time { font-size: 0.75rem; color: #aaa; margin-top: 3px; }
  .now .seek { display: block; width: 100%; margin: 6px 0 0; height: 24px; accent-color: #0a7; }
  [hidden] { display: none !important; }
  #pinOverlay { display: flex; position: fixed; inset: 0; background: rgba(0,0,0,0.85); z-index: 10; align-items: center; justify-content: center; }
  #pinOverlay .box { background: #222; padding: 20px; border-radius: 10px; text-align: center; }
  #pinInput { font-size: 2rem; width: 7em; text-align: center; letter-spacing: 0.3em; margin: 10px 0; }
  #msg { color: #f66; min-height: 1.2em; margin: -8px 0 8px; font-size: 0.9rem; }
  #pinMsg { color: #f66; min-height: 1.2em; font-size: 0.9rem; }
  /* Keep the now-playing panel and the buttons in view while scrolling a long list (not on very short screens) */
  @media (min-height: 500px) {
    header { position: sticky; top: 0; z-index: 5; background: #111; padding-top: 4px; }
  }
</style>
</head>
<body>
<div id="pinOverlay" hidden>
  <form class="box" onsubmit="unlock(); return false;">
    <div>Enter PIN (4-8 digits)</div>
    <input id="pinInput" type="password" inputmode="numeric" pattern="[0-9]{4,8}" minlength="4" maxlength="8" autocomplete="off">
    <div><button class="primary" id="unlockBtn" type="submit">Unlock</button></div>
    <div id="pinMsg"></div>
  </form>
</div>
<h1>OnlyM Remote</h1>
<header>
<div class="now">
  <img id="nowImg" alt="" style="visibility:hidden">
  <div class="info">
    <div class="label" id="nowLabel">Loading...</div>
    <div class="title" id="nowTitle"></div>
    <div class="bar" id="nowBarWrap" style="display:none"><div id="nowBar"></div></div>
    <input class="seek" type="range" id="seek" min="0" max="0" step="0.1" value="0" aria-label="Position" hidden>
    <div class="time" id="nowTime"></div>
  </div>
</div>
<div class="controls">
  <button class="primary" onclick="api('/api/previous')" title="Previous" aria-label="Previous">&#x23EE;</button>
  <button class="primary" id="pauseBtn" onclick="api('/api/pause')" title="Pause" aria-label="Pause">&#x23F8;</button>
  <button class="primary" onclick="api('/api/stop')" title="Stop" aria-label="Stop">&#x23F9;</button>
  <button class="primary" onclick="api('/api/next')" title="Next" aria-label="Next">&#x23ED;</button>
  <button id="volBtn" title="Volume" aria-label="Volume" aria-haspopup="true" aria-expanded="false" aria-controls="volPop" hidden>&#x1F50A;</button>
  <button onclick="refreshAll()" title="Refresh" aria-label="Refresh">&#x27F3;</button>
  <div class="volpop" id="volPop" role="group" aria-label="Volume" hidden>
    <button id="muteBtn" type="button">Mute</button>
    <input type="range" id="vol" min="0" max="100" step="5" aria-label="Volume level">
    <span id="volText"></span>
  </div>
</div>
<div id="msg"></div>
</header>
<div class="media-grid" id="grid"></div>
<script>
let media = [];
let volDragging = false;
let seekDragging = false;
let seekDragEnd = null;
let seekPending = null; // { value, until }: position sent on release, shown until the server reports it
let lastDuration = 0;
let lastActiveId = null;
const $ = id => document.getElementById(id);
let locked = false;

function showPin() {
  if (locked) {
    return;
  }
  locked = true;
  $('pinOverlay').hidden = false;
  $('pinInput').value = '';
  $('pinInput').focus();
  showLocked();
}
function showLocked() {
  $('nowLabel').textContent = 'Locked';
  $('nowTitle').textContent = 'PIN required';
  $('nowTime').textContent = '';
  $('nowBarWrap').style.display = 'none';
}
async function call(path, opts) {
  const r = await fetch(path, opts);
  if (r.status === 401) { showPin(); throw new Error('PIN required'); }
  return r;
}
let lockTimer = null;
function setPinEnabled(on) {
  $('pinInput').disabled = !on;
  $('unlockBtn').disabled = !on;
}
function startLockout(seconds) {
  clearInterval(lockTimer);
  let left = Math.max(1, Math.ceil(seconds || 60));
  setPinEnabled(false);
  const tick = () => {
    if (left <= 0) {
      clearInterval(lockTimer);
      lockTimer = null;
      $('pinMsg').textContent = '';
      setPinEnabled(true);
      $('pinInput').focus();
      return;
    }
    $('pinMsg').textContent = 'Too many attempts, try again in ' + left + ' s';
    left--;
  };
  tick();
  lockTimer = setInterval(tick, 1000);
}
async function unlock() {
  if (lockTimer) {
    return;
  }
  const r = await fetch('/api/login', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ pin: $('pinInput').value }) });
  let body = {};
  try { body = await r.json(); } catch (e) { }
  $('pinInput').value = '';
  if (r.ok) {
    locked = false;
    $('pinMsg').textContent = '';
    $('pinOverlay').hidden = true;
    refreshAll();
  } else if (r.status === 429) {
    startLockout(body.retryAfter || Number(r.headers.get('Retry-After')) || 60);
  } else {
    const n = body.attemptsLeft;
    $('pinMsg').textContent = typeof n === 'number' ? 'Wrong PIN, ' + n + (n === 1 ? ' attempt' : ' attempts') + ' left' : 'Wrong PIN';
  }
}

function esc(s) {
  return String(s).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);
}
function fmt(sec) {
  sec = Math.max(0, Math.floor(sec || 0));
  const m = Math.floor(sec / 60), s = sec % 60;
  return m + ':' + String(s).padStart(2, '0');
}
function volIcon(level) {
  // Same thresholds as the operator's volume button: muted, then low / medium / high
  return level <= 0 ? '\u{1F507}' : level <= 33 ? '\u{1F508}' : level <= 66 ? '\u{1F509}' : '\u{1F50A}';
}
function showVolume(level) {
  $('vol').value = level;
  $('volText').textContent = level + '%';
  $('volBtn').textContent = volIcon(level);
  $('muteBtn').textContent = level <= 0 ? 'Unmute' : 'Mute';
}
function setVolPop(open) {
  $('volPop').hidden = !open;
  $('volBtn').setAttribute('aria-expanded', open ? 'true' : 'false');
  if (!open) {
    volDragging = false;
  }
}
function initVolume() {
  const v = $('vol');
  v.addEventListener('pointerdown', () => volDragging = true);
  v.addEventListener('pointerup', () => volDragging = false);
  v.addEventListener('input', () => { volDragging = true; showVolume(Number(v.value)); });
  v.addEventListener('change', async () => { volDragging = false; await api('/api/volume/' + v.value); });
  $('volBtn').addEventListener('click', () => setVolPop($('volPop').hidden));
  $('muteBtn').addEventListener('click', () => api('/api/mute'));
  // Close on a tap outside the pop-over (or its button) and on Escape
  document.addEventListener('pointerdown', e => {
    if (!$('volPop').hidden && !$('volPop').contains(e.target) && !$('volBtn').contains(e.target)) {
      setVolPop(false);
    }
  });
  document.addEventListener('keydown', e => {
    if (e.key === 'Escape' && !$('volPop').hidden) {
      setVolPop(false);
      $('volBtn').focus();
    }
  });
}
function showSeekTime(pos) {
  $('nowTime').textContent = fmt(pos) + ' / ' + fmt(lastDuration);
}
function initSeek() {
  const s = $('seek');
  const endDrag = () => {
    // A tap that doesn't move the thumb fires no change event; stop ignoring the poll shortly after release
    clearTimeout(seekDragEnd);
    seekDragEnd = setTimeout(() => seekDragging = false, 300);
  };
  s.addEventListener('pointerdown', () => { clearTimeout(seekDragEnd); seekDragging = true; });
  s.addEventListener('pointerup', endDrag);
  s.addEventListener('pointercancel', endDrag);
  // Readout follows the thumb; the position is only sent on release
  s.addEventListener('input', () => { seekDragging = true; showSeekTime(Number(s.value)); });
  s.addEventListener('change', async () => {
    clearTimeout(seekDragEnd);
    seekDragging = false;
    const value = Number(s.value);
    seekPending = { value: value, until: Date.now() + 3000 };
    if (!await api('/api/seek/' + value.toFixed(1))) {
      seekPending = null; // e.g. 409 because it started playing; the next poll shows the real position
    }
  });
}
let msgTimer = null;
function showMsg(text) {
  $('msg').textContent = text;
  clearTimeout(msgTimer);
  if (text) {
    msgTimer = setTimeout(() => { $('msg').textContent = ''; }, 4000);
  }
}
async function api(path) {
  let ok = false;
  try {
    const r = await call(path, { method: 'POST' });
    ok = r.ok;
    if (!r.ok) {
      let body = {};
      try { body = await r.json(); } catch (e) { }
      showMsg(body.error || ('Request failed (' + r.status + ')'));
    } else {
      showMsg('');
    }
  } catch (e) {
    if (!locked) {
      showMsg('Connection lost');
    }
    return false;
  }
  setTimeout(refreshAll, 200);
  return ok;
}
function renderList(activeId) {
  $('grid').innerHTML = media.map(m => `
    <div class="media-item ${m.id === activeId ? 'active' : ''}" onclick="api('/api/play/${m.id}')">
      <img src="/api/thumbnails/${m.id}" alt="" onerror="this.style.visibility='hidden'">
      <div class="text">
        <div class="name">${esc(m.name || m.id)}</div>
        ${m.subtitle ? `<div class="sub">${esc(m.subtitle)}</div>` : ''}
      </div>
    </div>
  `).join('');
}
function renderStatus(status) {
  const item = media.find(m => m.id === status.currentItemId);
  const canPause = !!item && (item.type === 'Video' || item.type === 'Audio');
  $('pauseBtn').disabled = !canPause;
  const pb = $('pauseBtn');
  const showPlay = !!status.isPaused;
  pb.innerHTML = showPlay ? '&#x25B6;' : '&#x23F8;';
  pb.title = pb.ariaLabel = showPlay ? 'Resume' : 'Pause';

  if (status.currentItemId) {
    $('nowLabel').textContent = status.isPaused ? 'Paused' : 'Now playing';
    $('nowTitle').textContent = item?.name || status.currentItemId;
    const img = $('nowImg');
    const src = '/api/thumbnails/' + status.currentItemId;
    if (img.dataset.src !== src) { img.dataset.src = src; img.src = src; }
    img.style.visibility = 'visible';
    img.onerror = () => img.style.visibility = 'hidden';
    renderPosition(status);
    if (!(status.durationSeconds > 0)) {
      $('nowTime').textContent = item?.type || '';
    }
    if (typeof status.slideCount === 'number' && status.slideCount > 0) {
      // e.g. "Slide 3 of 8" (currentSlideIndex is 0-based)
      $('nowTime').textContent = (status.isRolling ? 'Rolling slide ' : 'Slide ') + (status.currentSlideIndex + 1) + ' of ' + status.slideCount;
    }
  } else {
    $('nowLabel').textContent = 'Stopped';
    $('nowTitle').textContent = 'Nothing playing';
    $('nowImg').style.visibility = 'hidden';
    $('nowImg').dataset.src = '';
    $('nowBarWrap').style.display = 'none';
    $('seek').hidden = true;
    seekDragging = false;
    seekPending = null;
    $('nowTime').textContent = '';
  }

  $('volBtn').hidden = !status.volumeEnabled;
  if (!status.volumeEnabled) {
    setVolPop(false);
  }
  if (!volDragging && typeof status.volume === 'number') {
    showVolume(status.volume);
  }

  if (status.currentItemId !== lastActiveId) {
    lastActiveId = status.currentItemId;
    renderList(lastActiveId);
  }
}
function renderPosition(status) {
  const duration = status.durationSeconds || 0;
  lastDuration = duration;
  const s = $('seek');
  if (!(duration > 0)) {
    $('nowBarWrap').style.display = 'none';
    s.hidden = true;
    return;
  }
  if (!status.canSeek) {
    // Playing, or not seekable: plain progress bar (OnlyM only allows scrubbing while paused)
    seekDragging = false;
    seekPending = null;
    s.hidden = true;
    $('nowBarWrap').style.display = '';
    $('nowBar').style.width = Math.min(100, 100 * status.positionSeconds / duration) + '%';
    $('nowTime').textContent = fmt(status.positionSeconds) + ' / ' + fmt(duration);
    return;
  }
  $('nowBarWrap').style.display = 'none';
  s.hidden = false;
  s.max = duration;
  if (seekDragging) {
    showSeekTime(Number(s.value)); // don't move the thumb under the user's finger
    return;
  }
  if (seekPending) {
    if (Math.abs(status.positionSeconds - seekPending.value) <= 0.5 || Date.now() > seekPending.until) {
      seekPending = null; // confirmed (or given up waiting): follow the server again
    } else {
      showSeekTime(seekPending.value);
      return;
    }
  }
  s.value = status.positionSeconds;
  showSeekTime(status.positionSeconds);
}
async function pollStatus() {
  try {
    if (locked) {
      return;
    }
    const status = await (await call('/api/status')).json();
    renderStatus(status);
  } catch (e) {
    if (locked) { showLocked(); return; }
    $('nowLabel').textContent = 'Connection lost';
    $('nowTitle').textContent = e.message;
  }
}
async function refreshAll() {
  try {
    if (locked) {
      return;
    }
    media = await (await call('/api/media')).json();
    lastActiveId = undefined; // force list redraw
    await pollStatus();
  } catch (e) {
    if (locked) { showLocked(); return; }
    $('nowLabel').textContent = 'Connection lost';
    $('nowTitle').textContent = e.message;
  }
}
initVolume();
initSeek();
refreshAll();
setInterval(pollStatus, 500);   // what's playing, position, volume
setInterval(refreshAll, 10000);  // media list (rarely changes)
</script>
</body>
</html>
""";

        var buf = Encoding.UTF8.GetBytes(html);
        res.ContentLength64 = buf.Length;
        await res.OutputStream.WriteAsync(buf);
    }

    private async Task ListMedia(HttpListenerResponse res)
    {
        var items = await OnUi(() => _operatorViewModel.MediaItems.Select(BuildMediaEntry).ToList());

        await Ok(res, items);
    }

    private async Task Status(HttpListenerResponse res)
    {
        var status = await OnUi(() => BuildStatus(
            _operatorViewModel.MediaItems.FirstOrDefault(m => m.IsMediaActive),
            _optionsService.Volume,
            IsRemoteVolumeEnabled(_optionsService)));

        await Ok(res, status);
    }

    private async Task PlayById(string idStr, HttpListenerResponse res)
    {
        if (!Guid.TryParse(idStr, out var id))
        {
            res.StatusCode = 400;
            await Ok(res, new { error = Properties.Resources.REMOTE_CONTROL_API_INVALID_ID });
            return;
        }

        var exists = await OnUi(() => _operatorViewModel.MediaItems.Any(m => m.Id == id));
        if (!exists)
        {
            res.StatusCode = 404;
            await Ok(res, new { error = Properties.Resources.REMOTE_CONTROL_API_ITEM_NOT_FOUND });
            return;
        }

        await StartItem(id, res);
    }

    /// <summary>
    /// Starts an item the way an operator would: the operator window won't start video/audio while
    /// video/audio is playing (or anything while a video plays), so the active item is stopped first.
    /// Replies 409 with a message if the item could not be started.
    /// </summary>
    private async Task StartItem(Guid id, HttpListenerResponse res)
    {
        (Guid? activeId, bool mustStop, string name) = await OnUi(() =>
        {
            var target = _operatorViewModel.MediaItems.FirstOrDefault(m => m.Id == id);
            var active = _operatorViewModel.MediaItems.FirstOrDefault(m => m.IsMediaActive);
            var activeType = active?.MediaType?.Classification;
            var targetType = target?.MediaType?.Classification;
            var targetIsAv = targetType is Core.Models.MediaClassification.Video or Core.Models.MediaClassification.Audio;
            // The operator window won't start anything but audio while a rolling slideshow runs, so stop that too
            var stop = active != null && active.Id != id &&
                       (activeType == Core.Models.MediaClassification.Video ||
                        (activeType == Core.Models.MediaClassification.Audio && targetIsAv) ||
                        (active.IsRollingSlideshow && targetType != Core.Models.MediaClassification.Audio));
            return (active?.Id, stop, target?.Title ?? id.ToString());
        });

        // Playing the item that is already active would toggle it off, so ignore it.
        if (activeId == id)
        {
            await Ok(res, new { success = true, id, alreadyActive = true });
            return;
        }

        if (mustStop && activeId is { } stopId)
        {
            await OnUiAsync(() => _operatorViewModel.MediaControlCommand1.ExecuteAsync(stopId));
            var stopped = await WaitUntil(
                () => !_operatorViewModel.MediaItems.Any(m => m.IsMediaChanging || (m.Id == stopId && m.IsMediaActive)),
                TimeSpan.FromSeconds(5));
            if (!stopped)
            {
                res.StatusCode = 409;
                await Ok(res, new { error = Properties.Resources.REMOTE_CONTROL_API_STOP_TIMEOUT });
                return;
            }
        }

        await OnUiAsync(() => _operatorViewModel.MediaControlCommand1.ExecuteAsync(id));
        var started = await WaitUntil(
            () => _operatorViewModel.MediaItems.Any(m => m.Id == id && m.IsMediaActive),
            TimeSpan.FromSeconds(3));
        if (!started)
        {
            res.StatusCode = 409;
#pragma warning disable CA1863
            await Ok(res, new { error = string.Format(CultureInfo.CurrentCulture, Properties.Resources.REMOTE_CONTROL_API_COULD_NOT_START, name) });
#pragma warning restore CA1863
            return;
        }

        await Ok(res, new { success = true, id, alreadyActive = false });
    }

    private static async Task<bool> WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        var end = DateTime.UtcNow + timeout;
        while (true)
        {
            if (await OnUi(condition))
            {
                return true;
            }

            if (DateTime.UtcNow >= end)
            {
                return false;
            }

            await Task.Delay(100);
        }
    }

    private async Task StopCurrent(HttpListenerResponse res)
    {
        await OnUiAsync(async () =>
        {
            var active = _operatorViewModel.MediaItems.FirstOrDefault(m => m.IsMediaActive);
            if (active != null)
            {
                await _operatorViewModel.MediaControlCommand1.ExecuteAsync(active.Id);
            }
        });

        await Ok(res);
    }

    private async Task TogglePause(HttpListenerResponse res)
    {
        await OnUiAsync(async () =>
        {
            var active = _operatorViewModel.MediaItems.FirstOrDefault(m => m.IsMediaActive);
            if (active != null)
            {
                await _operatorViewModel.MediaControlPauseCommand.ExecuteAsync(active.Id);
            }
        });

        await Ok(res);
    }

    private Task PlayNext(HttpListenerResponse res) => PlayRelative(res, +1);

    private Task PlayPrevious(HttpListenerResponse res) => PlayRelative(res, -1);

    private async Task PlayRelative(HttpListenerResponse res, int step)
    {
        // A slideshow works like a presentation clicker: Next/Previous change slide while it can move that way,
        // then move on to the next/previous media item.
        var slide = await OnUi(() =>
        {
            var active = _operatorViewModel.MediaItems.FirstOrDefault(m => m.IsMediaActive);
            return ShouldChangeSlide(active, step) ? (Id: active!.Id, Index: active.CurrentSlideshowIndex) : ((Guid Id, int Index)?)null;
        });

        if (slide is { } s)
        {
            await ChangeSlide(res, s.Id, s.Index, step);
            return;
        }

        var targetId = await OnUi(() =>
        {
            var items = _operatorViewModel.MediaItems.ToList();
            if (items.Count == 0)
            {
                return (Guid?)null;
            }

            var activeIndex = items.FindIndex(m => m.IsMediaActive);
            int index;
            if (activeIndex < 0)
            {
                index = step > 0 ? 0 : items.Count - 1;
            }
            else
            {
                index = (((activeIndex + step) % items.Count) + items.Count) % items.Count;
            }

            return items[index].Id;
        });

        if (targetId == null)
        {
            res.StatusCode = 409;
            await Ok(res, new { error = Properties.Resources.REMOTE_CONTROL_API_NO_MEDIA_ITEMS });
            return;
        }

        await StartItem(targetId.Value, res);
    }

    private async Task ChangeSlide(HttpListenerResponse res, Guid slideshowId, int oldIndex, int step)
    {
        var newIndex = await OnUi(() =>
        {
            var command = step > 0 ? _operatorViewModel.NextSlideCommand : _operatorViewModel.PreviousSlideCommand;
            command.Execute(slideshowId);
            return _operatorViewModel.MediaItems.FirstOrDefault(m => m.Id == slideshowId)?.CurrentSlideshowIndex;
        });

        if (newIndex == null || newIndex == oldIndex)
        {
            // e.g. a slide transition (or other media change) is still in progress
            res.StatusCode = 409;
            await Ok(res, new { error = step > 0 ? Properties.Resources.REMOTE_CONTROL_API_NEXT_SLIDE_FAILED : Properties.Resources.REMOTE_CONTROL_API_PREVIOUS_SLIDE_FAILED });
            return;
        }

        await Ok(res, new { success = true, id = slideshowId, slideIndex = newIndex });
    }

    private async Task SetVolume(string valueStr, HttpListenerResponse res)
    {
        if (!int.TryParse(valueStr, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var volume) ||
            volume is < 0 or > 100)
        {
            res.StatusCode = 400;
            await Ok(res, new { error = Properties.Resources.REMOTE_CONTROL_API_BAD_VOLUME });
            return;
        }

        await OnUi(() => _optionsService.Volume = volume);
        await Ok(res, new { success = true, volume });
    }

    private async Task ToggleMute(HttpListenerResponse res)
    {
        // The operator's mute button: mutes, or restores the level that was in use before muting
        var volume = await OnUi(() =>
        {
            _operatorViewModel.ToggleMuteCommand.Execute(null);
            return _optionsService.Volume;
        });

        await Ok(res, new { success = true, volume });
    }

    private async Task Seek(string secondsText, HttpListenerResponse res)
    {
        // Checked and applied in one UI-thread step so the item can't start playing in between. Setting the
        // position is exactly what the operator's slider does: the paused frame moves if "Allow scrubbing when
        // paused" is on, and either way playback resumes from the new position.
        var (statusCode, error, positionSeconds) = await OnUi(() =>
        {
            var active = _operatorViewModel.MediaItems.FirstOrDefault(m => m.IsMediaActive);
            var plan = PlanSeek(active, secondsText);
            if (plan.StatusCode == 200)
            {
                active!.PlaybackPositionDeciseconds = plan.Deciseconds;
            }

            return (plan.StatusCode, plan.Error, plan.Deciseconds / 10.0);
        });

        if (statusCode != 200)
        {
            res.StatusCode = statusCode;
            await Ok(res, new { error });
            return;
        }

        await Ok(res, new { success = true, positionSeconds });
    }

    private async Task ServeThumbnail(string idStr, HttpListenerResponse res)
    {
        if (!Guid.TryParse(idStr, out var id))
        {
            res.StatusCode = 400;
            await Ok(res, new { error = Properties.Resources.REMOTE_CONTROL_API_INVALID_ID });
            return;
        }

        var (filePath, classification) = await OnUi(() =>
        {
            var item = _operatorViewModel.MediaItems.FirstOrDefault(m => m.Id == id);
            return (item?.FilePath, item?.MediaType?.Classification ?? Core.Models.MediaClassification.Unknown);
        });

        if (filePath == null)
        {
            res.StatusCode = 404;
            await Ok(res, new { error = Properties.Resources.REMOTE_CONTROL_API_THUMBNAIL_NOT_AVAILABLE });
            return;
        }

        try
        {
            // Thumbnail generation can be slow, so it stays on the request thread, not the UI thread.
            var lastChanged = File.Exists(filePath) ? File.GetLastWriteTimeUtc(filePath).Ticks : 0;
            var bytes = _thumbnailService.GetThumbnail(
                filePath,
                App.FMpegFolderName,
                classification,
                lastChanged,
                out _);

            if (bytes == null || bytes.Length == 0)
            {
                res.StatusCode = 404;
                await Ok(res, new { error = Properties.Resources.REMOTE_CONTROL_API_THUMBNAIL_NOT_FOUND });
                return;
            }

            res.ContentType = "image/jpeg";
            res.ContentLength64 = bytes.Length;
            await res.OutputStream.WriteAsync(bytes);
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Thumbnail generation failed for {Id}", id);
            res.StatusCode = 500;
            await Ok(res, new { error = ex.Message });
        }
    }

    // MediaItems and the view-model commands belong to the WPF UI thread, and
    // HttpListener requests arrive on thread-pool threads, so marshal every access.
    private static Task<T> OnUi<T>(Func<T> func)
        => System.Windows.Application.Current.Dispatcher.InvokeAsync(func).Task;

    private static Task OnUiAsync(Func<Task> func)
        => System.Windows.Application.Current.Dispatcher.InvokeAsync(func).Task.Unwrap();

    private static Task Ok(HttpListenerResponse res, object? data = null)
        => WriteJson(res, data ?? new { success = true });

    private static async Task WriteJson(HttpListenerResponse res, object data)
    {
        var json = SerializeJson(data);
        var buf = Encoding.UTF8.GetBytes(json);
        res.ContentLength64 = buf.Length;
        await res.OutputStream.WriteAsync(buf);
    }
}
