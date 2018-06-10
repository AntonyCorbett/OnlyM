namespace OnlyM.Models
{
    using System;

    internal class FilesCopyProgressEventArgs : EventArgs
    {
        public string FilePath { get; set; }

        public FileCopyStatus Status { get; set; }

        public double PercentageComplete { get; set; }
    }
}
