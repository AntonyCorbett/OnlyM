namespace OnlyM.Core.Subtitles
{
    internal sealed class SubtitleEntry
    {
        public int Number { get; set; }

        public SubtitleTiming? Timing { get; set; }

        public string? Text { get; set; }
    }
}
