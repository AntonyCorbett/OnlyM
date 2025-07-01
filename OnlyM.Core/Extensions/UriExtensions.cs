using System;

namespace OnlyM.Core.Extensions;

internal static class UriExtensions
{
    public static string ToRelative(this Uri uri) =>
        uri.IsAbsoluteUri ? uri.PathAndQuery : uri.OriginalString;

    public static string? ToAbsolute(this Uri relativeUri, string baseUrl)
    {
        var baseUri = new Uri(baseUrl);
        return relativeUri.ToAbsolute(baseUri);
    }

    public static string? ToAbsolute(this Uri relativeUri, Uri baseUri)
    {
        var relative = relativeUri.ToRelative();

        if (Uri.TryCreate(baseUri, relative, out var absolute))
        {
            return absolute.ToString();
        }

        return relativeUri.IsAbsoluteUri ? relativeUri.ToString() : null;
    }
}
