using OnlyM.Core.Extensions;

namespace OnlyM.Core.Tests;
public class StringExtensionsTests
{
    [Theory]
    [InlineData("123abc", "123")]
    [InlineData("456", "456")]
    [InlineData("abc123", "")]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("789xyz123", "789")]
    [InlineData("0start", "0")]
    [InlineData("   ", "")]
    [InlineData("12 34", "12")]
    [InlineData("!@#123", "")]
    public void GetNumericPrefix_ReturnsExpectedResult(string? input, string? expected)
    {
        var result = input.GetNumericPrefix();
        Assert.Equal(expected, result);
    }
}
