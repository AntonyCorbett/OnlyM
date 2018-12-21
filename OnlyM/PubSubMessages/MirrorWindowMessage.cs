namespace OnlyM.PubSubMessages
{
    using System;

    internal class MirrorWindowMessage
    {
        public Guid MediaItemId { get; set; }

        public bool UseMirror { get; set; }
    }
}
