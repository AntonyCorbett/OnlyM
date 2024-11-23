using System;

namespace OnlyM.PubSubMessages;

internal sealed class MirrorWindowMessage
{
    public Guid MediaItemId { get; set; }

    public bool UseMirror { get; set; }
}
