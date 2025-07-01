using System;
using System.Globalization;

namespace OnlyM.Core.Extensions;

public static class TimeSpanExtensions
{
    // note that timeSpan should be less than 24 hrs
    public static string AsMediaDurationString(this TimeSpan timeSpan) =>
        timeSpan.ToString(@"hh\:mm\:ss", CultureInfo.CurrentCulture);
}
