using System;
using System.Globalization;
using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;

namespace OnlyM;

internal static class AppCenterInit
{
    // Please omit this token (or use your own) if you are building a fork
    private static readonly string? TheToken = "78e43751-d0c9-4fc7-8137-0fcc95ebd0fe";

    public static void Execute()
    {
        if (OperatingSystem.IsWindows())
        {
            AppCenter.Start(TheToken, typeof(Analytics), typeof(Crashes));
            AppCenter.SetCountryCode(RegionInfo.CurrentRegion.TwoLetterISORegionName);
        }
    }
}
