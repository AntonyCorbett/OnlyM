using System.Globalization;
using OnlyM.Core.Extensions;

namespace OnlyM.Core.Tests;

public class TimeSpanExtensionsTests
{
    [Theory]
    [InlineData(0, 0, 0, "00:00:00")]
    [InlineData(1, 2, 3, "01:02:03")]
    [InlineData(12, 34, 56, "12:34:56")]
    [InlineData(23, 59, 59, "23:59:59")]
    [InlineData(99, 59, 59, "03:59:59")] // we ignore days!
    public void AsMediaDurationString_ReturnsExpectedFormat(int hours, int minutes, int seconds, string expected)
    {
        // Arrange
        var ts = new TimeSpan(hours, minutes, seconds);

        // Act
        var result = ts.AsMediaDurationString();

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void AsMediaDurationString_UsesCurrentCulture()
    {
        // Arrange
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            var ts = new TimeSpan(1, 2, 3);

            // Act
            var result = ts.AsMediaDurationString();

            // Assert
            Assert.Equal("01:02:03", result);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}
