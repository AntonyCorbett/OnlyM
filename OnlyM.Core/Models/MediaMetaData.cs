using System;

namespace OnlyM.Core.Models;

public class MediaMetaData
{
    public string? Title { get; set; }

    public TimeSpan Duration { get; set; }

    public int VideoRotation { get; set; } // only used for videos
}
