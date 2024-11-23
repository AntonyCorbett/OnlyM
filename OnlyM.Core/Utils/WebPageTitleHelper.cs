using System;
using HtmlAgilityPack;

namespace OnlyM.Core.Utils;

public static class WebPageTitleHelper
{
    public static string? Get(Uri webPageAddress)
    {
        using (var wc = WebUtils.CreateWebClient())
        {
            var pageHtml = wc.DownloadString(webPageAddress);
            if (string.IsNullOrEmpty(pageHtml))
            {
                return null;
            }

            var document = new HtmlDocument();
            document.LoadHtml(pageHtml);
            return document.DocumentNode?.SelectSingleNode("html/head/title")?.InnerText;
        }
    }
}
