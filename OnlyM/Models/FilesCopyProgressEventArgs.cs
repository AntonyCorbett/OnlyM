using System;

namespace OnlyM.Models
{
    internal class FilesCopyProgressEventArgs : EventArgs
    {
        public FileCopyStatus Status { get; set; }
    }
}
