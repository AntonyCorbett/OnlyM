namespace OnlyM.Models
{
    using System;

    public class SlideTransitionEventArgs : EventArgs
    {
        public Guid MediaItemId { get; set; }

        public SlideTransition Transition { get; set; }
    }
}
