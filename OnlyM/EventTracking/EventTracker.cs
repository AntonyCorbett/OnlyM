using System;
using System.Collections.Generic;
using OnlyM.Core.Models;
using Sentry;

namespace OnlyM.EventTracking;

internal static class EventTracker
{
    public static void Track(EventName eventName, Dictionary<string, string>? properties = null)
    {
        SentrySdk.CaptureMessage(eventName.ToString(), SentryLevel.Info);
            
        SentrySdk.AddBreadcrumb(
            message: eventName.ToString(),
            category: "event",
            data: properties);
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
            SentrySdk.CaptureException(ex);
        }
        else
        {
            SentrySdk.CaptureException(ex, scope =>
            {
                scope.SetTag("context", context);
            });
        }
    }
}
