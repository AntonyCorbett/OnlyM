using System;

namespace OnlyM.PubSubMessages;

internal sealed class MirrorWindowMessage
{
    public Guid MediaItemId { get; init; }

    public bool UseMirror { get; init; }
}
