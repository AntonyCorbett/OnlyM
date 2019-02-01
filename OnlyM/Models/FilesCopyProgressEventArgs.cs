namespace OnlyM.Models
{
    using System;

    internal class FilesCopyProgressEventArgs : EventArgs
    {
        public FileCopyStatus Status { get; set; }
    }
}
