using System;
using System.Net.Http;
using System.Reflection;
using Serilog;

namespace OnlyM.AutoUpdates;

/// <summary>
/// Used to get the installed OnlyM version and the 
/// latest OnlyM release version from the GitHub webpage.
/// </summary>
internal static class VersionDetection
{
    public static string LatestReleaseUrl => "https://github.com/AntonyCorbett/OnlyM/releases/latest";

    public static Version? GetLatestReleaseVersion()
    {
        var versionString = GetLatestReleaseVersionString();

        if (string.IsNullOrEmpty(versionString))
        {
            return null;
        }

        var tokens = versionString.Split('.');
        if (tokens.Length != 4)
        {
            return null;
        }

        if (!int.TryParse(tokens[0], out var major) ||
            !int.TryParse(tokens[1], out var minor) ||
            !int.TryParse(tokens[2], out var build) ||
            !int.TryParse(tokens[3], out var revision))
        {
            return null;
        }

        return new Version(major, minor, build, revision);
    }

    public static string GetCurrentVersionString()
    {
        var ver = GetCurrentVersion();
        return ver == null ? "Unknown" : $"{ver.Major}.{ver.Minor}.{ver.Build}.{ver.Revision}";
    }

    public static Version? GetCurrentVersion() =>
        Assembly.GetExecutingAssembly().GetName().Version;

    private static string? GetLatestReleaseVersionString()
    {
        string? version = null;

        try
        {
#pragma warning disable U2U1025
            using var client = new HttpClient();
#pragma warning restore U2U1025

            var response = client.GetAsync(LatestReleaseUrl).Result;
            if (response.IsSuccessStatusCode)
            {
                var latestVersionUri = response.RequestMessage?.RequestUri;
                if (latestVersionUri != null)
                {
                    var segments = latestVersionUri.Segments;
                    if (segments.Length > 0)
                    {
                        version = segments[^1];
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Getting latest release version");
        }

        return version;
    }
}
