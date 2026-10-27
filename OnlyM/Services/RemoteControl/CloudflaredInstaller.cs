using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace OnlyM.Services.RemoteControl;

/// <summary>
/// Installs a pinned cloudflared release into <see cref="CloudflareTunnel.UserInstallDir"/> (per user, no admin needed).
/// The download goes to a temporary file next to the target, its SHA-256 is checked against the value published in the
/// release notes, and only then is it moved into place, so a partial or tampered download is never used.
/// </summary>
internal static class CloudflaredInstaller
{
    /// <summary>
    /// The pinned cloudflared release. To update: pick a release from https://github.com/cloudflare/cloudflared/releases
    /// and copy the SHA-256 values of the two Windows executables from its "SHA256 Checksums" section.
    /// </summary>
    public const string Version = "2026.10.4";

    // From https://github.com/cloudflare/cloudflared/releases/tag/2026.10.4 ("SHA256 Checksums")
    private const string Amd64Sha256 = "411b16d40c8d872243efeb52c550053d00af0a8a97fbb32e730fb8aba429eaa8";
    private const string X86Sha256 = "6d38146a071184959adde89c299e87132d89777ee5318dcb066e6fac861ec63c";

    private const string DownloadBaseUrl = "https://github.com/cloudflare/cloudflared/releases/download/";

    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromMinutes(5) };

    public static string InstallPath => Path.Combine(CloudflareTunnel.UserInstallDir, "cloudflared.exe");

    /// <summary>
    /// Gets the download URL and expected SHA-256 of the pinned cloudflared executable for this OS
    /// (the amd64 build also runs on Windows on Arm).
    /// </summary>
    public static (Uri Url, string Sha256) GetDownload(bool is64BitOperatingSystem)
    {
        var asset = is64BitOperatingSystem ? "cloudflared-windows-amd64.exe" : "cloudflared-windows-386.exe";
        return (new Uri($"{DownloadBaseUrl}{Version}/{asset}"), is64BitOperatingSystem ? Amd64Sha256 : X86Sha256);
    }

    /// <summary>
    /// Downloads, verifies and installs cloudflared.
    /// </summary>
    /// <returns>The full path of the installed cloudflared.exe.</returns>
    public static async Task<string> InstallAsync(CancellationToken cancellationToken = default)
    {
        var (url, sha256) = GetDownload(Environment.Is64BitOperatingSystem);
        var dir = CloudflareTunnel.UserInstallDir;
        Directory.CreateDirectory(dir);

        // Same folder as the target, so the final move is a rename on the same volume
        var tempFile = Path.Combine(dir, $"cloudflared.{Guid.NewGuid():N}.download");
        try
        {
            Log.Logger.Information("Downloading cloudflared {Version} from {Url}", Version, url);
            using (var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var file = new FileStream(tempFile, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
                await source.CopyToAsync(file, cancellationToken);
            }

            if (!await HasSha256Async(tempFile, sha256, cancellationToken))
            {
                throw new InvalidDataException($"The downloaded cloudflared does not have the expected SHA-256 ({sha256})");
            }

            File.Move(tempFile, InstallPath, overwrite: true);
            Log.Logger.Information("cloudflared {Version} verified and installed at {Path}", Version, InstallPath);
            return InstallPath;
        }
        finally
        {
            DeleteIfExists(tempFile);
        }
    }

    /// <summary>
    /// Gets a value indicating whether the file's SHA-256 equals the expected hex string (case-insensitive).
    /// </summary>
    internal static async Task<bool> HasSha256Async(string path, string expectedSha256, CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).Equals(expectedSha256, StringComparison.OrdinalIgnoreCase);
    }

    private static void DeleteIfExists(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Logger.Warning(ex, "Could not delete temporary file {Path}", path);
        }
    }
}
