namespace OnlyM.Core.Utils
{
    using System;
    using System.Linq;
    using System.Net;
    using HtmlAgilityPack;
    using OnlyM.Core.Extensions;

    internal static class FaviconHelper
    {
        public static byte[] GetIconImage(string websiteUrl)
        {
            return GetIconImage(websiteUrl, GetFaviconUrlFromHtml(websiteUrl)) ?? 
                   GetIconImage(websiteUrl, GetFaviconUrlFromRoot(websiteUrl));
        }

        private static byte[] GetIconImage(string websiteUrl, string iconUrl)
        {
            if (iconUrl == null)
            {
                return null;
            }

            try
            {
                using (var wc = WebUtils.CreateWebClient())
                {
                    var uri = new Uri(iconUrl, UriKind.RelativeOrAbsolute);
                    if (!uri.IsAbsoluteUri)
                    {
                        iconUrl = uri.ToAbsolute(GetRootUrl(websiteUrl));
                    }

                    return wc.DownloadData(iconUrl);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string GetRootUrl(string websiteUrl)
        {
            var uri = new Uri(websiteUrl);
            if (uri.HostNameType == UriHostNameType.Dns)
            {
                return $"{(uri.Scheme == "https" ? "https" : "http")}://{uri.Host}/";
            }

            return null;
        }

        private static string GetFaviconUrlFromRoot(string websiteUrl)
        {
            var uri = new Uri(websiteUrl);
            if (uri.HostNameType == UriHostNameType.Dns)
            {
                var icon = $"{(uri.Scheme == "https" ? "https" : "http")}://{uri.Host}/favicon.ico";
                if (UrlExists(icon))
                {
                    return icon;
                }
            }

            return null;
        }

        private static string GetFaviconUrlFromHtml(string websiteUrl)
        {
            using (var wc = WebUtils.CreateWebClient())
            {
                var pageHtml = wc.DownloadString(websiteUrl);
                var htmlDocument = new HtmlDocument();
                htmlDocument.LoadHtml(pageHtml);

                var appleIcon = htmlDocument.DocumentNode.SelectNodes("//link[contains(@rel, 'apple-touch-icon')]");
                if (appleIcon != null && appleIcon.Any())
                {
                    var favicon = appleIcon.First();
                    var icon = favicon.GetAttributeValue("href", null);
                    if (!string.IsNullOrWhiteSpace(icon))
                    {
                        return icon;
                    }
                }

                var elements = htmlDocument.DocumentNode.SelectNodes("//link[contains(@rel, 'icon')]");
                if (elements != null && elements.Any())
                {
                    var favicon = elements.First();
                    var icon = favicon.GetAttributeValue("href", null);
                    if (!string.IsNullOrWhiteSpace(icon))
                    {
                        return icon;
                    }
                }

                return null;
            }
        }

        private static bool UrlExists(string url)
        {
            try
            {
                var webRequest = WebRequest.Create(url);
                webRequest.Method = "HEAD";
                var webResponse = (HttpWebResponse)webRequest.GetResponse();
                return webResponse.StatusCode == HttpStatusCode.OK;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
