# Remote Control

OnlyM can optionally expose a simple HTTP remote so another device (phone, tablet, or another PC) can control media playback.

The feature is **off by default**.

## Enabling

1. Open **Settings**.
2. Turn on **Remote Control**.
3. Choose a mode:
   - **Local LAN** – for devices on the same network.
   - **Cloudflare** – for temporary access from outside the local network (no Cloudflare account required).

## Local LAN mode

- OnlyM listens on the port shown in Settings (default 8088).
- The local URL and a QR code are displayed in Settings.
- If Windows Firewall blocks the port, use the **Create firewall exception** button. This runs an elevated command to allow the selected port. You will be prompted for administrator permission. You can change the port first if needed.
- Changing the port takes effect immediately (no restart of OnlyM needed): the listener (and the Cloudflare tunnel, if running) restarts on the new port when you leave the port box. Devices must then open the new URL. If you use the firewall exception, click the button again for the new port.

## Cloudflare mode

- OnlyM can start a Cloudflare Quick Tunnel.
- A temporary public URL is shown (and available via QR code).
- The URL changes every time the tunnel is started. It is not permanent.
- Requires the `cloudflared` binary. You can install or start it from the Settings page (elevated action if needed).
- A PIN of at least 6 digits is always required in this mode. If none is set, OnlyM generates a random 6-digit PIN (shown in Settings). If the existing PIN is shorter than 6 digits, the tunnel is not started and Settings shows "Cloudflare tunnel not started: the PIN must be at least 6 digits"; enter a longer PIN and the tunnel starts automatically.
- OnlyM runs `cloudflared tunnel --url http://localhost:<port> --http-host-header localhost:<port>`. The Host header is rewritten to `localhost:<port>` so Windows (http.sys) accepts the forwarded requests, including when OnlyM could only listen on `http://localhost:<port>/` (no URL reservation).

## Using the remote

Open the URL (or scan the QR code) in a browser on another device.

The page shows:
- Current media list with thumbnails
- Play / Stop / Next / Previous
- Volume control
- Current status

If a PIN is set, you will be asked for it when you open the page. A PIN is 4-8 digits in Local LAN mode (optional) and 6-8 digits in Cloudflare mode (required). You stay signed in while you use the remote; after 30 minutes without any request from the page, or 12 hours after signing in, the session expires and the page asks for the PIN again.

**Next** / **Previous** (and tapping an item) work while something is playing: like an operator would, the remote first stops a playing video or audio item, then starts the requested item. If the item can't be started, the page shows a short error message (the API returns HTTP 409 with an `error` text).

## Volume control in the operator window

The volume control sits on the left of the bottom toolbar of the main OnlyM window (opposite the folder, help and settings buttons) whenever the operator page is shown, whether or not Remote Control is enabled. It is hidden on the Settings page. It controls video and audio volume and stays in sync with the remote's volume control.

- **Speaker button.** The icon shows the current level: muted (0%), low (1-33%), medium (34-66%) or high (67-100%). Click it (or Tab to it and press Space/Enter) to mute; click it again to restore the previous level.
- **Slider.** Drag or click the slim slider, or focus it and use the arrow keys (5% steps) or Page Up/Page Down (10% steps). The percentage is shown while dragging, and in the tooltip.
- **Level label.** Below 100% a small label next to the slider shows the level (e.g. "60%") and the tooltip reads e.g. "Volume 60% - media will play quieter"; when muted the speaker icon is dimmed and the label and tooltip read "Muted". At 100% no label is shown.
- **How mute works.** Mute simply sets the volume to 0, so it is saved like any other volume setting and the remote's `/api/status` reports `volume: 0`. Setting the volume above 0 from anywhere (the slider, or the remote) unmutes. The level that unmute restores is kept in memory only: it is the last level that was held for at least a second before the volume went to 0, so dragging the slider down to 0 restores the level before the drag. If OnlyM is started while muted, unmute restores 100%.

## Security notes

- The feature is optional and off by default.
- Use a PIN if the remote will be accessible beyond a trusted network.
- **No CORS.** The remote page is served by OnlyM itself, so all its requests are same-origin. OnlyM sends no `Access-Control-Allow-*` headers, so other web sites can't read API responses.
- **Origin check.** Any `/api/*` request that has an `Origin` header must come from the same origin as the request's Host (or the current Cloudflare tunnel URL); otherwise it gets HTTP 403. This stops a web page open on any device on the network from sending "simple" cross-site POSTs (play, stop, volume) to the remote. Requests without an `Origin` header (e.g. curl, scripts) are allowed and still need the PIN if one is set.
- **Host check.** `/api/*` requests are only accepted when the Host is an IP address, `localhost`, this PC's name (also `<name>.local` and `<name>.<DNS domain>`), or the tunnel host; otherwise HTTP 403. This blocks DNS-rebinding attacks, where an outside site makes its own domain name resolve to this PC to get around the Origin check. If you open the remote through another DNS name or alias, use the IP address instead.
- **PIN in Local LAN mode is optional (decision).** The PIN stays optional on the LAN because the Origin and Host checks remove the browser-based attack paths (a malicious web page can't drive OnlyM), and requiring a PIN on a trusted venue network adds friction for operators. What remains is someone who can already reach the PC on the network and sends requests directly. On a network you don't fully trust (shared or guest Wi-Fi), set a PIN. Cloudflare mode always requires a PIN because the URL is reachable from the internet.
- **Login throttling is per client.** After 5 wrong PINs from one client, logins from that client are blocked for 60 seconds (HTTP 429 with `Retry-After`); other clients are not affected. Failures are forgotten after 15 minutes, a correct PIN resets the client's count, and at most 1000 clients (and 1000 /48 prefixes) are tracked (stale entries are pruned).
- **How a client is identified.** IPv4 clients are keyed by their full address; IPv4-mapped IPv6 addresses (`::ffff:a.b.c.d`) are treated as the IPv4 address. IPv6 clients usually control a whole /64 (and often a /48), so they could otherwise rotate addresses to get fresh attempts. An IPv6 client is therefore counted in two buckets at once: its /64 prefix (5 wrong PINs) and its /48 prefix (12 wrong PINs). Logins are blocked for 60 seconds when either bucket reaches its limit. A correct PIN resets only the /64 bucket; the /48 bucket ages out on its own, so a legitimate user cannot reset an attacker's count on a shared prefix. Tunnel and LAN clients are counted separately.
- **Global backstop.** Across all clients together, at most 20 wrong PINs are allowed in any rolling 10 minutes. Once that budget is used up, new login attempts get HTTP 429 (`{"error":"locked","retryAfter":n}` plus `Retry-After`, so the page shows its countdown) without the PIN being checked, until the oldest failure is more than 10 minutes old. The suggested wait starts at 5 seconds and doubles (5, 10, 20, 40, 60 s) while attempts keep arriving, capped at 60 seconds and at the time until a slot frees up; it resets once the budget is available again. Only wrong PINs count: successful logins, logged-in sessions and all other API calls are not affected.
- **Brute-force maths.** The global budget caps guessing at 20 per 10 minutes = 2,880 guesses per day, however many addresses an attacker uses. A 6-digit PIN has 1,000,000 values: on average an attacker needs 500,000 guesses (about 174 days, roughly 5.7 months) and at worst 1,000,000 (about 347 days). An 8-digit PIN takes on average about 95 years. A 4-digit PIN (Local mode only) would take on average about 1.7 days at the global limit, which is why Cloudflare mode requires at least 6 digits.
- **Client identity behind the tunnel.** Requests relayed by cloudflared arrive from `localhost`, so the real client address is taken from Cloudflare's `CF-Connecting-IP` header. That header is only trusted when the request comes from loopback while Cloudflare mode is active and the tunnel is running (and, if the browser sent an `Origin`, only when it is the tunnel URL). For all other requests (e.g. LAN clients) the TCP remote address is used, so the header can't be spoofed to get around the limit.
- **Sessions expire.** A session ends after 30 minutes without requests (sliding) and in any case 12 hours after login (absolute); an expired session gets HTTP 401 and the page shows the PIN prompt again. The session cookie is `HttpOnly; SameSite=Strict`, and changing the PIN signs out all remote sessions.
- Cloudflare Quick Tunnels are convenient but temporary and publicly reachable while running.
- The firewall exception is a user-initiated elevated action; it is not applied automatically.

## Limitations

- Windows-only for the firewall rule button.
- Cloudflare URLs are ephemeral.
- The remote page is intentionally simple (no advanced editing or media management).
- Login throttling can affect legitimate users: someone sharing an IPv6 /48 with an attacker (for example a relay or carrier egress range) can be locked out with them for 60 seconds, and while the global backstop is active (a sustained attack from many addresses) new logins - including the operator's - are refused until a slot frees up. Devices that are already logged in keep working. Restarting OnlyM or changing the PIN clears all counters.
