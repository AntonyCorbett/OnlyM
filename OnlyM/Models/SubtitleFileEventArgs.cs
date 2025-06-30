using System;

namespace OnlyM.Models;

internal sealed class SubtitleFileEventArgs
{
    public Guid MediaItemId { get; init; }

    public bool Starting { get; init; }
}
