using System;

namespace OnlyM.PubSubMessages;

internal sealed class SubtitleFileMessage
{
    public Guid MediaItemId { get; init; }

    public bool Starting { get; init; }
}
