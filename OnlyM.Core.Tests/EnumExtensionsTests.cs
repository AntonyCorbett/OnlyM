using OnlyM.Core.Extensions;
using OnlyM.Core.Models;
using OnlyM.Core.Properties;
using Serilog.Events;

namespace OnlyM.Core.Tests;

public class EnumExtensionsTests
{
    [Theory]
    [InlineData(MagnifierShape.Circle)]
    [InlineData(MagnifierShape.Square)]
    public void GetDescriptiveName_MagnifierShape_ReturnsExpected(MagnifierShape shape)
    {
        var expected = shape switch
        {
            MagnifierShape.Square => Resources.MAGNIFIER_SHAPE_SQUARE,
            _ => Resources.MAGNIFIER_SHAPE_CIRCLE
        };
        Assert.Equal(expected, shape.GetDescriptiveName());
    }

    [Theory]
    [InlineData(MagnifierSize.XXSmall)]
    [InlineData(MagnifierSize.XSmall)]
    [InlineData(MagnifierSize.Small)]
    [InlineData(MagnifierSize.Medium)]
    [InlineData(MagnifierSize.Large)]
    [InlineData(MagnifierSize.XLarge)]
    [InlineData(MagnifierSize.XXLarge)]
    public void GetDescriptiveName_MagnifierSize_ReturnsExpected(MagnifierSize size)
    {
        var expected = size switch
        {
            MagnifierSize.XXSmall => Resources.MAGNIFIER_SIZE_XXSMALL,
            MagnifierSize.XSmall => Resources.MAGNIFIER_SIZE_XSMALL,
            MagnifierSize.Small => Resources.MAGNIFIER_SIZE_SMALL,
            MagnifierSize.Medium => Resources.MAGNIFIER_SIZE_MEDIUM,
            MagnifierSize.Large => Resources.MAGNIFIER_SIZE_LARGE,
            MagnifierSize.XLarge => Resources.MAGNIFIER_SIZE_XLARGE,
            MagnifierSize.XXLarge => Resources.MAGNIFIER_SIZE_XXLARGE,
            _ => Resources.MAGNIFIER_SIZE_MEDIUM
        };
        Assert.Equal(expected, size.GetDescriptiveName());
    }

    [Theory]
    [InlineData((MagnifierSize)999)]
    public void GetDescriptiveName_MagnifierSize_DefaultCase_ReturnsMedium(MagnifierSize size)
    {
        Assert.Equal(Resources.MAGNIFIER_SIZE_MEDIUM, size.GetDescriptiveName());
    }

    [Theory]
    [InlineData(FadeSpeed.Slow, 2.0)]
    [InlineData(FadeSpeed.Normal, 1.0)]
    [InlineData(FadeSpeed.Fast, 0.75)]
    [InlineData(FadeSpeed.SuperFast, 0.2)]
    [InlineData((FadeSpeed)999, 1.0)] // default case
    public void GetFadeSpeedSeconds_ReturnsExpected(FadeSpeed speed, double expected)
    {
        Assert.Equal(expected, speed.GetFadeSpeedSeconds());
    }

    [Theory]
    [InlineData(FadeSpeed.Slow)]
    [InlineData(FadeSpeed.Normal)]
    [InlineData(FadeSpeed.Fast)]
    [InlineData(FadeSpeed.SuperFast)]
    public void GetDescriptiveName_FadeSpeed_ReturnsExpected(FadeSpeed speed)
    {
        var expected = speed switch
        {
            FadeSpeed.Slow => Resources.FADE_SPEED_SLOW,
            FadeSpeed.Fast => Resources.FADE_SPEED_FAST,
            FadeSpeed.SuperFast => Resources.FADE_SPEED_SUPER_FAST,
            _ => Resources.FADE_SPEED_NORMAL
        };
        Assert.Equal(expected, speed.GetDescriptiveName());
    }

    [Theory]
    [InlineData((FadeSpeed)999)]
    public void GetDescriptiveName_FadeSpeed_DefaultCase_ReturnsNormal(FadeSpeed speed)
    {
        Assert.Equal(Resources.FADE_SPEED_NORMAL, speed.GetDescriptiveName());
    }

    [Theory]
    [InlineData(LogEventLevel.Debug)]
    [InlineData(LogEventLevel.Error)]
    [InlineData(LogEventLevel.Fatal)]
    [InlineData(LogEventLevel.Verbose)]
    [InlineData(LogEventLevel.Warning)]
    [InlineData(LogEventLevel.Information)]
    public void GetDescriptiveName_LogEventLevel_ReturnsExpected(LogEventLevel level)
    {
        var expected = level switch
        {
            LogEventLevel.Debug => Resources.LOG_LEVEL_DEBUG,
            LogEventLevel.Error => Resources.LOG_LEVEL_ERROR,
            LogEventLevel.Fatal => Resources.LOG_LEVEL_FATAL,
            LogEventLevel.Verbose => Resources.LOG_LEVEL_VERBOSE,
            LogEventLevel.Warning => Resources.LOG_LEVEL_WARNING,
            _ => Resources.LOG_LEVEL_INFORMATION
        };
        Assert.Equal(expected, level.GetDescriptiveName());
    }

    [Theory]
    [InlineData((LogEventLevel)999)]
    public void GetDescriptiveName_LogEventLevel_DefaultCase_ReturnsInformation(LogEventLevel level)
    {
        Assert.Equal(Resources.LOG_LEVEL_INFORMATION, level.GetDescriptiveName());
    }

    [Theory]
    [InlineData(ImageFadeType.None)]
    [InlineData(ImageFadeType.FadeIn)]
    [InlineData(ImageFadeType.FadeOut)]
    [InlineData(ImageFadeType.FadeInOut)]
    [InlineData(ImageFadeType.CrossFade)]
    public void GetDescriptiveName_ImageFadeType_ReturnsExpected(ImageFadeType fadeType)
    {
        var expected = fadeType switch
        {
            ImageFadeType.None => Resources.FADE_NONE,
            ImageFadeType.FadeIn => Resources.FADE_IN,
            ImageFadeType.FadeOut => Resources.FADE_OUT,
            ImageFadeType.FadeInOut => Resources.FADE_IN_OUT,
            _ => Resources.FADE_CROSS
        };
        Assert.Equal(expected, fadeType.GetDescriptiveName());
    }

    [Theory]
    [InlineData((ImageFadeType)999)]
    public void GetDescriptiveName_ImageFadeType_DefaultCase_ReturnsCrossFade(ImageFadeType fadeType)
    {
        Assert.Equal(Resources.FADE_CROSS, fadeType.GetDescriptiveName());
    }
}
