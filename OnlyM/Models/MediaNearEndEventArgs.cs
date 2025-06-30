using System;

namespace OnlyM.Models;

public class MediaNearEndEventArgs : EventArgs
{
    public Guid MediaItemId { get; init; }
}
