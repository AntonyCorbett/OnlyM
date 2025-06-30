using System;
using OnlyM.Core.Extensions;

namespace OnlyM.Models;

internal sealed class RecentTimesItem
{
    public int Seconds { get; init; }

    public string AsString => ToString();

    public bool IsNotZero => Seconds != 0;

    public override string ToString()
    {
        return Seconds == 0
            ? Properties.Resources.CLEAR_RECENTS
            : TimeSpan.FromSeconds(Seconds).AsMediaDurationString();
    }
}
