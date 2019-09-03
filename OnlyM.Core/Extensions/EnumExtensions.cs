namespace OnlyM.Core.Extensions
{
    using OnlyM.Core.Models;
    using OnlyM.Core.Properties;
    using Serilog.Events;

    public static class EnumExtensions
    {
        public static string GetDescriptiveName(this MagnifierShape shape)
        {
            switch (shape)
            {
                case MagnifierShape.Square:
                    return Resources.MAGNIFIER_SHAPE_SQUARE;

                default:
                case MagnifierShape.Circle:
                    return Resources.MAGNIFIER_SHAPE_CIRCLE;
            }
        }

        public static string GetDescriptiveName(this MagnifierSize size)
        {
            switch (size)
            {
                case MagnifierSize.XXSmall:
                    return Resources.MAGNIFIER_SIZE_XXSMALL;

                case MagnifierSize.XSmall:
                    return Resources.MAGNIFIER_SIZE_XSMALL;

                case MagnifierSize.Small:
                    return Resources.MAGNIFIER_SIZE_SMALL;

                default:
                case MagnifierSize.Medium:
                    return Resources.MAGNIFIER_SIZE_MEDIUM;

                case MagnifierSize.Large:
                    return Resources.MAGNIFIER_SIZE_LARGE;

                case MagnifierSize.XLarge:
                    return Resources.MAGNIFIER_SIZE_XLARGE;

                case MagnifierSize.XXLarge:
                    return Resources.MAGNIFIER_SIZE_XXLARGE;
            }
        }

        public static double GetFadeSpeedSeconds(this FadeSpeed speed)
        {
            switch (speed)
            {
                case FadeSpeed.Slow:
                    return 2.0;

                case FadeSpeed.Fast:
                    return 0.75;

                case FadeSpeed.SuperFast:
                    return 0.2;

                default:
                // ReSharper disable once RedundantCaseLabel
                case FadeSpeed.Normal:
                    return 1.0;
            }
        }

        public static string GetDescriptiveName(this FadeSpeed speed)
        {
            switch (speed)
            {
                case FadeSpeed.Slow:
                    return Resources.FADE_SPEED_SLOW;
                    
                case FadeSpeed.Fast:
                    return Resources.FADE_SPEED_FAST;

                case FadeSpeed.SuperFast:
                    return Resources.FADE_SPEED_SUPER_FAST;

                default:
                // ReSharper disable once RedundantCaseLabel
                case FadeSpeed.Normal:
                    return Resources.FADE_SPEED_NORMAL;
            }
        }

        public static string GetDescriptiveName(this LogEventLevel level)
        {
            switch (level)
            {
                case LogEventLevel.Debug:
                    return Resources.LOG_LEVEL_DEBUG;
                    
                case LogEventLevel.Error:
                    return Resources.LOG_LEVEL_ERROR;
                    
                case LogEventLevel.Fatal:
                    return Resources.LOG_LEVEL_FATAL;
                    
                case LogEventLevel.Verbose:
                    return Resources.LOG_LEVEL_VERBOSE;
                    
                case LogEventLevel.Warning:
                    return Resources.LOG_LEVEL_WARNING;

                default:
                // ReSharper disable once RedundantCaseLabel
                case LogEventLevel.Information:
                    return Resources.LOG_LEVEL_INFORMATION;
            }
        }

        public static string GetDescriptiveName(this ImageFadeType fadeType)
        {
            switch (fadeType)
            {
                case ImageFadeType.None:
                    return Resources.FADE_NONE;

                case ImageFadeType.FadeIn:
                    return Resources.FADE_IN;

                case ImageFadeType.FadeOut:
                    return Resources.FADE_OUT;

                case ImageFadeType.FadeInOut:
                    return Resources.FADE_IN_OUT;

                default:
                // ReSharper disable once RedundantCaseLabel
                case ImageFadeType.CrossFade:
                    return Resources.FADE_CROSS;
            }
        }
    }
}
