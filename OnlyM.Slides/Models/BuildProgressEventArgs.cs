namespace OnlyM.Slides.Models
{
    using System;

    public class BuildProgressEventArgs : EventArgs
    {
        public string EntryName { get; set; }

        public double PercentageComplete { get; set; }
    }
}
