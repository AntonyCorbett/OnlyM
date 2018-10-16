namespace OnlyM.Core.Subtitles
{
    using System;

    public class SubtitleEventArgs : EventArgs
    {
        public SubtitleStatus Status { get; set; }

        public string Text { get; set; }
    }
}
