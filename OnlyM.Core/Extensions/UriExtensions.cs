using System;

namespace OnlyM.Core.Extensions;

internal static class UriExtensions
{
    public static string ToRelative(this Uri uri) =>
        uri.IsAbsoluteUri ? uri.PathAndQuery : uri.OriginalString;

    public static string? ToAbsolute(this Uri uri, string baseUrl)
    {
        var baseUri = new Uri(baseUrl);
        return uri.ToAbsolute(baseUri);
    }

    public static string? ToAbsolute(this Uri uri, Uri baseUri)
    {
        var relative = uri.ToRelative();

        if (Uri.TryCreate(baseUri, relative, out var absolute))
        {
            return absolute.ToString();
        }

        return uri.IsAbsoluteUri ? uri.ToString() : null;
    }
}
