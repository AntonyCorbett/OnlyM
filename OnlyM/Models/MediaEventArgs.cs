using System;
using OnlyM.Core.Models;

namespace OnlyM.Models;

public class MediaEventArgs : EventArgs
{
    public Guid MediaItemId { get; init; }

    public MediaClassification Classification { get; init; }

    public MediaChange Change { get; init; }
}
