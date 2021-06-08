using System;

namespace OnlyM.Core.Subtitles
{
    public class SubtitleEventArgs : EventArgs
    {
        public SubtitleStatus Status { get; set; }

        public string? Text { get; set; }
    }
}
