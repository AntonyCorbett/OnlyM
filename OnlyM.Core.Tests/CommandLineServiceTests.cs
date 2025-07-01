using System.Reflection;
using OnlyM.Core.Services.CommandLine;

namespace OnlyM.Core.Tests;

public class CommandLineServiceTests
{
    [Fact]
    public void Constructor_DefaultArgs_SetsDefaults()
    {
        // Arrange
        var originalArgs = Environment.GetCommandLineArgs();
        var testArgs = new[] { "app.exe" };
        SetCommandLineArgs(testArgs);

        // Act
        var service = new CommandLineService();

        // Assert
        Assert.False(service.NoGpu);
        Assert.Null(service.OptionsIdentifier);
        Assert.False(service.NoSettings);
        Assert.False(service.NoFolder);
        Assert.Null(service.SourceFolder);
        Assert.False(service.DisableVideoRenderingFix);

        // Cleanup
        SetCommandLineArgs(originalArgs);
    }

    [Fact]
    public void Constructor_WithAllArgs_SetsProperties()
    {
        // Arrange
        var originalArgs = Environment.GetCommandLineArgs();
        var testArgs = new[]
        {
            "app.exe",
            "--nogpu",
            "--id=abc",
            "--nosettings",
            "--nofolder",
            "--source=.",
            "--novidfix"
        };
        SetCommandLineArgs(testArgs);

        // Act
        var service = new CommandLineService();

        // Assert
        Assert.True(service.NoGpu);
        Assert.Equal("abc", service.OptionsIdentifier);
        Assert.True(service.NoSettings);
        Assert.True(service.NoFolder);
        Assert.Equal(Path.GetFullPath("."), service.SourceFolder);
        Assert.True(service.DisableVideoRenderingFix);

        // Cleanup
        SetCommandLineArgs(originalArgs);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("relative/path")]
    public void GetFullSourcePath_VariousInputs_ReturnsExpected(string? input)
    {
        // Act
        var result = CommandLineService.GetFullSourcePath(input);

        // Assert
        if (string.IsNullOrEmpty(input))
        {
            Assert.Null(result);
        }
        else
        {
            Assert.Equal(Path.GetFullPath(input), result);
        }
    }

    [Fact]
    public void GetFullSourcePath_InvalidPath_ReturnsNull()
    {
        // Use an invalid path (e.g., invalid chars)
        var invalidPath = new string(Path.GetInvalidPathChars()) + "invalid";

        // Act
        var result = CommandLineService.GetFullSourcePath(invalidPath);

        // Assert
        Assert.Null(result);
    }

    // Helper to set command line args for testing
    private static void SetCommandLineArgs(string[] args)
    {
        typeof(Environment)
            .GetField("s_commandLineArgs", BindingFlags.Static | BindingFlags.NonPublic)
            ?.SetValue(null, args);
    }
}
