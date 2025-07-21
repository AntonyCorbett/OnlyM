using System;
using System.Collections.Generic;
using OnlyM.Core.Models;
using Sentry;

namespace OnlyM.EventTracking;

internal static class EventTracker
{
    // https://soundbox.sentry.io/
    // https://docs.sentry.io/platforms/dotnet/guides/wpf/

    public static void AddBreadcrumb(
        EventName eventName, string category, Dictionary<string, string>? properties = null)
    {
        SentrySdk.AddBreadcrumb(
            message: eventName.ToString(),
            category: category,
            data: properties);
    }

    public static void AddStartMediaBreadcrumb(SupportedMediaType? mediaType)
    {
        AddStartOrStopMediaBreadcrumb(mediaType, true);
    }

    public static void AddStopMediaBreadcrumb(SupportedMediaType? mediaType)
    {
        AddStartOrStopMediaBreadcrumb(mediaType, false);
    }

    public static void Error(Exception ex, string? context = null)
    {
        if (string.IsNullOrEmpty(context))
        {
            SentrySdk.CaptureException(ex);
        }
        else
        {
            SentrySdk.CaptureException(ex, scope => { scope.SetTag("context", context); });
        }
    }

    public static void Error(string message, string? context = null)
    {
        if (string.IsNullOrEmpty(context))
        {
            SentrySdk.CaptureMessage(message, SentryLevel.Error);
        }
        else
        {
            SentrySdk.CaptureMessage(message, scope => { scope.SetTag("context", context); }, SentryLevel.Error);
        }
    }

    private static void AddStartOrStopMediaBreadcrumb(SupportedMediaType? mediaType, bool start)
    {
        var properties = new Dictionary<string, string>
        {
            { "type", mediaType?.Classification.ToString() ?? "No Type" },
        };

        var startStopAsString = start ? "start" : "stop";
        AddBreadcrumb(start ? EventName.StartMedia : EventName.StopMedia, $"media.{startStopAsString}", properties);
    }
}
