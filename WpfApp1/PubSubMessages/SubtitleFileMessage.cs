namespace OnlyM.PubSubMessages
{
    using System;

    internal class SubtitleFileMessage
    {
        public Guid MediaItemId { get; set; }

        public bool Starting { get; set; }
    }
}
