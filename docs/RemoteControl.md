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

## Cloudflare mode

- OnlyM can start a Cloudflare Quick Tunnel.
- A temporary public URL is shown (and available via QR code).
- The URL changes every time the tunnel is started. It is not permanent.
- Requires the `cloudflared` binary. You can install or start it from the Settings page (elevated action if needed).

## Using the remote

Open the URL (or scan the QR code) in a browser on another device.

The page shows:
- Current media list with thumbnails
- Play / Stop / Next / Previous
- Volume control
- Current status

If a PIN is set (4 digits), you will be asked for it once per browser session.

## Volume control in the operator window

The volume slider on the main OnlyM operator page is only visible when Remote Control is enabled.

## Security notes

- The feature is optional and off by default.
- Use a PIN if the remote will be accessible beyond a trusted network.
- Cloudflare Quick Tunnels are convenient but temporary and publicly reachable while running.
- The firewall exception is a user-initiated elevated action; it is not applied automatically.

## Limitations

- Windows-only for the firewall rule button.
- Cloudflare URLs are ephemeral.
- The remote page is intentionally simple (no advanced editing or media management).
