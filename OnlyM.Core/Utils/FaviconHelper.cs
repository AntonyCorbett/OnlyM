using System;
using System.Linq;
using System.Net;
using HtmlAgilityPack;
using OnlyM.Core.Extensions;

namespace OnlyM.Core.Utils;

internal static class FaviconHelper
{
    public static byte[]? GetIconImage(string? websiteUrl) =>
        GetIconImage(websiteUrl, GetFaviconUrlFromHtml(websiteUrl)) ??
        GetIconImage(websiteUrl, GetFaviconUrlFromRoot(websiteUrl));

    private static byte[]? GetIconImage(string? websiteUrl, string? iconUrl)
    {
        if (websiteUrl == null || iconUrl == null)
        {
            return null;
        }

        try
        {
            using var wc = WebUtils.CreateWebClient();
            var uri = new Uri(iconUrl, UriKind.RelativeOrAbsolute);
            if (!uri.IsAbsoluteUri)
            {
                var rootUrl = GetRootUrl(websiteUrl);
                if (rootUrl == null)
                {
                    return null;
                }

                iconUrl = uri.ToAbsolute(rootUrl);
            }

            return iconUrl == null ? null : wc.DownloadData(iconUrl);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? GetRootUrl(string? websiteUrl)
    {
        if (websiteUrl == null)
        {
            return null;
        }

        var uri = new Uri(websiteUrl);
        if (uri.HostNameType == UriHostNameType.Dns)
        {
            return $"{(uri.Scheme == "https" ? "https" : "http")}://{uri.Host}/";
        }

        return null;
    }

    private static string? GetFaviconUrlFromRoot(string? websiteUrl)
    {
        if (websiteUrl == null)
        {
            return null;
        }

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

    private static string? GetFaviconUrlFromHtml(string? websiteUrl)
    {
        if (websiteUrl == null)
        {
            return null;
        }

        using var wc = WebUtils.CreateWebClient();
        var pageHtml = wc.DownloadString(websiteUrl);
        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(pageHtml);

        var appleIcon = htmlDocument.DocumentNode.SelectNodes("//link[contains(@rel, 'apple-touch-icon')]");
        if (appleIcon != null && appleIcon.Count > 0)
        {
            var favicon = appleIcon.First();
            var icon = favicon.GetAttributeValue("href", null);
            if (!string.IsNullOrWhiteSpace(icon))
            {
                return icon;
            }
        }

        var elements = htmlDocument.DocumentNode.SelectNodes("//link[contains(@rel, 'icon')]");
        if (elements != null && elements.Count > 0)
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

    private static bool UrlExists(string url)
    {
        try
        {
            // todo: update to HttpClient, ensuring we use sync rather than async to avoid multiple code changes
#pragma warning disable SYSLIB0014 // Type or member is obsolete
            var webRequest = WebRequest.Create(url);
#pragma warning restore SYSLIB0014 // Type or member is obsolete
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
