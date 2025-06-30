using System;

namespace OnlyM.Core.Subtitles;

public class SubtitleEventArgs : EventArgs
{
    public SubtitleStatus Status { get; init; }

    public string? Text { get; init; }
}
