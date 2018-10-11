namespace OnlyM.Models
{
    using System;

    public class SlideTransitionEventArgs : EventArgs
    {
        public Guid MediaItemId { get; set; }

        public int OldSlideIndex { get; set; }

        public int NewSlideIndex { get; set; }

        public SlideTransition Transition { get; set; }
    }
}
