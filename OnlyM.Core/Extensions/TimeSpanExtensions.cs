namespace OnlyM.Core.Extensions
{
    using System;

    public static class TimeSpanExtensions
    {
        public static string AsMediaDurationString(this TimeSpan timeSpan)
        {
            return timeSpan.ToString(@"hh\:mm\:ss");
        }
    }
}
