using System;

namespace OnlyM.Slides.Models
{
    public class BuildProgressEventArgs : EventArgs
    {
        public string EntryName { get; set; }

        public double PercentageComplete { get; set; }
    }
}
