using System;

namespace OnlyM.Slides.Models;

public class BuildProgressEventArgs : EventArgs
{
    public string? EntryName { get; init; }

    public double PercentageComplete { get; init; }
}
