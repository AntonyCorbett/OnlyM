namespace OnlyM.AutoUpdates
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Reflection;
    using Serilog;

    /// <summary>
    /// Used to get the installed OnlyM version and the 
    /// latest OnlyM release version from the github webpage.
    /// </summary>
    internal static class VersionDetection
    {
        public static string LatestReleaseUrl => "https://github.com/AntonyCorbett/OnlyM/releases/latest";

        public static string GetLatestReleaseVersionString()
        {
            string version = null;

            try
            {
                using (var client = new HttpClient())
                {
                    var response = client.GetAsync(LatestReleaseUrl).Result;
                    if (response.IsSuccessStatusCode)
                    {
                        var latestVersionUri = response.RequestMessage.RequestUri;
                        if (latestVersionUri != null)
                        {
                            var segments = latestVersionUri.Segments;
                            if (segments.Any())
                            {
                                version = segments[segments.Length - 1];
                            }
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

        public static Version GetLatestReleaseVersion()
        {
            string versionString = GetLatestReleaseVersionString();

            if (string.IsNullOrEmpty(versionString))
            {
                return null;
            }

            string[] tokens = versionString.Split('.');
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
            return $"{ver.Major}.{ver.Minor}.{ver.Build}.{ver.Revision}";
        }

        public static Version GetCurrentVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version;
        }
    }
}
