using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using OnlyM.Core.Services.Media;
using OnlyM.Core.Services.Options;
using OnlyM.Core.Utils;
using OnlyM.Models;
using OnlyM.ViewModel;

namespace OnlyM.Services.RemoteControl;

internal sealed class RemoteControlService : IRemoteControlService, IDisposable
{
    private const string SessionCookieName = "onlym_session";
    private const string CfConnectingIpHeader = "CF-Connecting-IP";

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

    private void StartCore(string? prefix)
    {
        if (_running)
        {
            return;
        }

        // Listen on all interfaces so devices on the LAN can reach the remote.
        // Requires a one-time URL reservation (as admin): netsh http add urlacl url=http://+:<port>/ user=Everyone
        prefix ??= $"http://+:{_optionsService.RemoteControlPort}/";
        HttpListener listener;
        try
        {
            listener = CreateAndStart(prefix);
        }
        catch (HttpListenerException ex) when (ex.ErrorCode == 5 && !prefix.Contains("://localhost:", StringComparison.OrdinalIgnoreCase))
        {
            // Access denied: listening on anything but localhost needs a URL reservation
            // (netsh http add urlacl url=<prefix> user=Everyone). Fall back so the remote still works locally.
            var fallback = $"http://localhost:{_optionsService.RemoteControlPort}/";
            Log.Logger.Warning(
                "Remote control could not listen on {Prefix} (access denied; run 'netsh http add urlacl url={Prefix} user=Everyone' as admin). Falling back to {Fallback}",
                prefix,
                prefix,
                fallback);
            prefix = fallback;
            listener = CreateAndStart(prefix);
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
            var path = req.Url?.AbsolutePath.TrimEnd('/') ?? "";

            // Serve HTML remote control page at root
            if ((path == "" || path == "/" || path == "/remote") && req.HttpMethod == "GET")
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
                await Ok(res, new { error = "Forbidden" });
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
                await Ok(res, new { error = "PIN required" });
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
                    else if (path.StartsWith("/api/volume/", StringComparison.Ordinal) && req.HttpMethod == "POST")
                    {
                        await SetVolume(path["/api/volume/".Length..], res);
                    }
                    else if (path.StartsWith("/api/thumbnails/", StringComparison.Ordinal) && req.HttpMethod == "GET")
                    {
                        var idStr = path["/api/thumbnails/".Length..];
                        await ServeThumbnail(idStr, res);
                    }
                    else
                    {
                        res.StatusCode = 404;
                        await Ok(res, new { error = "Not found" });
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
  .media-item .name { flex: 1 1 auto; min-width: 0; padding: 6px 8px; font-size: 0.95rem; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
  button:disabled { opacity: 0.4; cursor: default; }
  .volume { display: flex; align-items: center; gap: 10px; margin-bottom: 16px; }
  .volume input { flex: 1 1 auto; accent-color: #0a7; height: 28px; }
  .volume span { min-width: 3em; text-align: right; }
  .now { display: flex; align-items: center; gap: 12px; margin-bottom: 12px; padding: 10px; background: #222; border-radius: 8px; }
  .now img { width: 96px; height: 54px; object-fit: cover; background: #000; border-radius: 4px; }
  .now .info { flex: 1 1 auto; min-width: 0; }
  .now .label { font-size: 0.75rem; color: #9a9; text-transform: uppercase; letter-spacing: 0.05em; }
  .now .title { font-size: 1.05rem; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
  .now .bar { height: 4px; background: #444; border-radius: 2px; margin-top: 6px; overflow: hidden; }
  .now .bar div { height: 100%; width: 0; background: #0a7; }
  .now .time { font-size: 0.75rem; color: #aaa; margin-top: 3px; }
  [hidden] { display: none !important; }
  #pinOverlay { display: flex; position: fixed; inset: 0; background: rgba(0,0,0,0.85); z-index: 10; align-items: center; justify-content: center; }
  #pinOverlay .box { background: #222; padding: 20px; border-radius: 10px; text-align: center; }
  #pinInput { font-size: 2rem; width: 7em; text-align: center; letter-spacing: 0.3em; margin: 10px 0; }
  #msg { color: #f66; min-height: 1.2em; margin: -8px 0 8px; font-size: 0.9rem; }
  #pinMsg { color: #f66; min-height: 1.2em; font-size: 0.9rem; }
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
<div class="now">
  <img id="nowImg" alt="" style="visibility:hidden">
  <div class="info">
    <div class="label" id="nowLabel">Loading...</div>
    <div class="title" id="nowTitle"></div>
    <div class="bar" id="nowBarWrap" style="display:none"><div id="nowBar"></div></div>
    <div class="time" id="nowTime"></div>
  </div>
</div>
<div class="controls">
  <button class="primary" onclick="api('/api/previous')" title="Previous" aria-label="Previous">&#x23EE;</button>
  <button class="primary" id="pauseBtn" onclick="api('/api/pause')" title="Pause" aria-label="Pause">&#x23F8;</button>
  <button class="primary" onclick="api('/api/stop')" title="Stop" aria-label="Stop">&#x23F9;</button>
  <button class="primary" onclick="api('/api/next')" title="Next" aria-label="Next">&#x23ED;</button>
  <button onclick="refreshAll()" title="Refresh" aria-label="Refresh">&#x27F3;</button>
</div>
<div id="msg"></div>
<div class="volume">&#x1F50A; <input type="range" id="vol" min="0" max="100" step="5"> <span id="volText"></span></div>
<div class="media-grid" id="grid"></div>
<script>
let media = [];
let volDragging = false;
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
function initVolume() {
  const v = $('vol');
  v.addEventListener('pointerdown', () => volDragging = true);
  v.addEventListener('pointerup', () => volDragging = false);
  v.addEventListener('input', () => $('volText').textContent = v.value + '%');
  v.addEventListener('change', async () => { volDragging = false; await call('/api/volume/' + v.value, { method: 'POST' }); });
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
  try {
    const r = await call(path, { method: 'POST' });
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
    return;
  }
  setTimeout(refreshAll, 200);
}
function renderList(activeId) {
  $('grid').innerHTML = media.map(m => `
    <div class="media-item ${m.id === activeId ? 'active' : ''}" onclick="api('/api/play/${m.id}')">
      <img src="/api/thumbnails/${m.id}" alt="" onerror="this.style.visibility='hidden'">
      <div class="name">${esc(m.name || m.id)}</div>
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
    if (status.durationSeconds > 0) {
      $('nowBarWrap').style.display = '';
      $('nowBar').style.width = Math.min(100, 100 * status.positionSeconds / status.durationSeconds) + '%';
      $('nowTime').textContent = fmt(status.positionSeconds) + ' / ' + fmt(status.durationSeconds);
    } else {
      $('nowBarWrap').style.display = 'none';
      $('nowTime').textContent = item?.type || '';
    }
  } else {
    $('nowLabel').textContent = 'Stopped';
    $('nowTitle').textContent = 'Nothing playing';
    $('nowImg').style.visibility = 'hidden';
    $('nowImg').dataset.src = '';
    $('nowBarWrap').style.display = 'none';
    $('nowTime').textContent = '';
  }

  if (!volDragging && typeof status.volume === 'number') {
    $('vol').value = status.volume;
    $('volText').textContent = status.volume + '%';
  }

  if (status.currentItemId !== lastActiveId) {
    lastActiveId = status.currentItemId;
    renderList(lastActiveId);
  }
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
        var items = await OnUi(() => _operatorViewModel.MediaItems
            .Select(m => new
            {
                id = m.Id.ToString(),
                name = m.Title ?? Path.GetFileNameWithoutExtension(m.FilePath ?? ""),
                type = m.MediaType?.Classification.ToString() ?? "Unknown",
                durationSeconds = m.DurationDeciseconds / 10.0,
                isActive = m.IsMediaActive,
                isPaused = m.IsPaused,
                hasThumbnail = m.FilePath != null
            })
            .ToList());

        await Ok(res, items);
    }

    private async Task Status(HttpListenerResponse res)
    {
        var status = await OnUi(() =>
        {
            var active = _operatorViewModel.MediaItems.FirstOrDefault(m => m.IsMediaActive);
            return new
            {
                isPlaying = active != null && !active.IsPaused,
                isPaused = active?.IsPaused ?? false,
                currentItemId = active?.Id.ToString(),
                positionSeconds = (active?.PlaybackPositionDeciseconds ?? 0) / 10.0,
                durationSeconds = (active?.DurationDeciseconds ?? 0) / 10.0,
                volume = _optionsService.Volume
            };
        });

        await Ok(res, status);
    }

    private async Task PlayById(string idStr, HttpListenerResponse res)
    {
        if (!Guid.TryParse(idStr, out var id))
        {
            res.StatusCode = 400;
            await Ok(res, new { error = "Invalid id" });
            return;
        }

        var exists = await OnUi(() => _operatorViewModel.MediaItems.Any(m => m.Id == id));
        if (!exists)
        {
            res.StatusCode = 404;
            await Ok(res, new { error = "Media item not found" });
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
            var stop = active != null && active.Id != id &&
                       (activeType == Core.Models.MediaClassification.Video ||
                        (activeType == Core.Models.MediaClassification.Audio && targetIsAv));
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
                await Ok(res, new { error = "The current item did not stop in time" });
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
            await Ok(res, new { error = $"Could not start \"{name}\"" });
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
                index = ((activeIndex + step) % items.Count + items.Count) % items.Count;
            }

            return items[index].Id;
        });

        if (targetId == null)
        {
            res.StatusCode = 409;
            await Ok(res, new { error = "No media items" });
            return;
        }

        await StartItem(targetId.Value, res);
    }

    private async Task SetVolume(string valueStr, HttpListenerResponse res)
    {
        if (!int.TryParse(valueStr, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var volume) ||
            volume is < 0 or > 100)
        {
            res.StatusCode = 400;
            await Ok(res, new { error = "Volume must be 0-100" });
            return;
        }

        await OnUi(() => _optionsService.Volume = volume);
        await Ok(res, new { success = true, volume });
    }

    private async Task ServeThumbnail(string idStr, HttpListenerResponse res)
    {
        if (!Guid.TryParse(idStr, out var id))
        {
            res.StatusCode = 400;
            await Ok(res, new { error = "Invalid id" });
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
            await Ok(res, new { error = "Thumbnail not available" });
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
                await Ok(res, new { error = "Thumbnail not found" });
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
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var buf = Encoding.UTF8.GetBytes(json);
        res.ContentLength64 = buf.Length;
        await res.OutputStream.WriteAsync(buf);
    }
}
