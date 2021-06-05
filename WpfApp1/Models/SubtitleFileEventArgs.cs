namespace OnlyM.Models
{
    using System;

    internal class SubtitleFileEventArgs
    {
        public Guid MediaItemId { get; set; }

        public bool Starting { get; set; }
    }
}
