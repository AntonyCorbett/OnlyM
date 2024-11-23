using System;

namespace OnlyM.Models;

internal sealed class SubtitleFileEventArgs
{
    public Guid MediaItemId { get; set; }

    public bool Starting { get; set; }
}
