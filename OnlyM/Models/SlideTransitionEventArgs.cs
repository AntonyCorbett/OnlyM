using System;

namespace OnlyM.Models;

public class SlideTransitionEventArgs : EventArgs
{
    public Guid MediaItemId { get; init; }

    public int OldSlideIndex { get; init; }

    public int NewSlideIndex { get; init; }

    public SlideTransition Transition { get; init; }
}
