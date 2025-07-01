using OnlyM.Core.Extensions;

namespace OnlyM.Core.Tests;

public class UriExtensionsTests
{
    [Theory]
    [InlineData("https://example.com/path?query=1", "/path?query=1")]
    [InlineData("/relative/path", "/relative/path")]
    [InlineData("relative/path", "relative/path")]
    public void ToRelative_ReturnsExpectedResult(string uriString, string expected)
    {
        var uri = new Uri(uriString, Uri.IsWellFormedUriString(uriString, UriKind.Absolute) ? UriKind.Absolute : UriKind.Relative);
        var result = uri.ToRelative();
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("/path?query=1", "https://example.com", "https://example.com/path?query=1")]
    [InlineData("relative/path", "https://example.com/base/", "https://example.com/base/relative/path")]
    [InlineData("https://other.com/abc", "https://example.com", "https://example.com/abc")]
    public void ToAbsolute_WithBaseUrl_ReturnsExpectedResult(string uriString, string baseUrl, string expected)
    {
        var uri = new Uri(uriString, Uri.IsWellFormedUriString(uriString, UriKind.Absolute) ? UriKind.Absolute : UriKind.Relative);
        var result = uri.ToAbsolute(baseUrl);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("/path?query=1", "https://example.com", "https://example.com/path?query=1")]
    [InlineData("relative/path", "https://example.com/base/", "https://example.com/base/relative/path")]
    [InlineData("https://other.com/abc", "https://example.com", "https://example.com/abc")]
    public void ToAbsolute_WithBaseUri_ReturnsExpectedResult(string uriString, string baseUriString, string expected)
    {
        var uri = new Uri(uriString, Uri.IsWellFormedUriString(uriString, UriKind.Absolute) ? UriKind.Absolute : UriKind.Relative);
        var baseUri = new Uri(baseUriString, UriKind.Absolute);
        var result = uri.ToAbsolute(baseUri);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ToAbsolute_WithAbsoluteUri_Treats_RelativeUri_As_Relative()
    {
        var relativeUri = new Uri("https://example.com/path", UriKind.Absolute);
        var baseUri = new Uri("https://other.com", UriKind.Absolute);
        var result = relativeUri.ToAbsolute(baseUri);
        Assert.Equal("https://other.com/path", result);
    }
}
