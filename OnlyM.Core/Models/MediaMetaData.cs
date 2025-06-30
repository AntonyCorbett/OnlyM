using System;

namespace OnlyM.Core.Models;

public class MediaMetaData
{
    public string? Title { get; init; }

    public TimeSpan Duration { get; init; }

    public int VideoRotation { get; init; } // only used for videos
}
