namespace OnlyM.Core.Subtitles;

internal sealed class SubtitleEntry
{
    public int Number { get; init; }

    public SubtitleTiming? Timing { get; init; }

    public string? Text { get; init; }
}
