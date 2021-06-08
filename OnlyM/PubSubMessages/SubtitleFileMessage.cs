using System;

namespace OnlyM.PubSubMessages
{
    internal class SubtitleFileMessage
    {
        public Guid MediaItemId { get; set; }

        public bool Starting { get; set; }
    }
}
