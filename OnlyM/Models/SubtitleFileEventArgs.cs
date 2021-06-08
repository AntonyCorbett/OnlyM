using System;

namespace OnlyM.Models
{
    internal class SubtitleFileEventArgs
    {
        public Guid MediaItemId { get; set; }

        public bool Starting { get; set; }
    }
}
