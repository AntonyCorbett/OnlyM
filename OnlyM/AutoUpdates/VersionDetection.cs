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

        public static string GetLatestReleaseVersion()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    var response = client.GetAsync(LatestReleaseUrl).Result;
                    if (!response.IsSuccessStatusCode)
                    {
                        return null;
                    }

                    var latestVersionUri = response.RequestMessage.RequestUri;
                    if (latestVersionUri == null)
                    {
                        return null;
                    }

                    var segments = latestVersionUri.Segments;
                    if (!segments.Any())
                    {
                        return null;
                    }

                    return segments[segments.Length - 1];
                }
            }
            // ReSharper disable once CatchAllClause
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Getting latest release version");
                return null;
            }
        }

        public static string GetCurrentVersion()
        {
            var ver = Assembly.GetExecutingAssembly().GetName().Version;
            return $"{ver.Major}.{ver.Minor}.{ver.Build}.{ver.Revision}";
        }
    }
}
