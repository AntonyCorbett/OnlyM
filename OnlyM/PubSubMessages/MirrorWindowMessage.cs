using System;

namespace OnlyM.PubSubMessages
{
    internal class MirrorWindowMessage
    {
        public Guid MediaItemId { get; set; }

        public bool UseMirror { get; set; }
    }
}
