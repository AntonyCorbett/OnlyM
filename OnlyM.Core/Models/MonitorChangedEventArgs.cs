using System;

namespace OnlyM.Core.Models;

public class MonitorChangedEventArgs : EventArgs
{
    public MonitorChangeDescription Change { get; init; }
}
