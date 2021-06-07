using System;

namespace OnlyM.Core.Extensions
{
    public static class TimeSpanExtensions
    {
        public static string AsMediaDurationString(this TimeSpan timeSpan)
        {
            return timeSpan.ToString(@"hh\:mm\:ss");
        }
    }
}
