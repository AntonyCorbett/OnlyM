using System;

namespace OnlyM.PubSubMessages
{
    internal sealed class SubtitleFileMessage
    {
        public Guid MediaItemId { get; set; }

        public bool Starting { get; set; }
    }
}
