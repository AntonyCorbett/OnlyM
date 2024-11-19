using System;
using System.Collections.Generic;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using OnlyM.Core.Models;

namespace OnlyM.EventTracking;

internal static class EventTracker
{
    public static void Track(EventName eventName, Dictionary<string, string>? properties = null)
    {
        Analytics.TrackEvent(eventName.ToString(), properties);
    }

    public static void TrackStartMedia(SupportedMediaType? mediaType)
    {
        var properties = new Dictionary<string, string>
        {
            { "type", mediaType?.Classification.ToString() ?? "No Type" },
        };

        Track(EventName.StartMedia, properties);
    }

    public static void Error(Exception ex, string? context = null)
    {
        if (string.IsNullOrEmpty(context))
        {
            Crashes.TrackError(ex);
        }
        else
        {
            var properties = new Dictionary<string, string> { { "context", context } };
            Crashes.TrackError(ex, properties);
        }
    }
}
