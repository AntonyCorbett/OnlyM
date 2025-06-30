using System;

namespace OnlyM.Models;

internal sealed class FilesCopyProgressEventArgs : EventArgs
{
    public FileCopyStatus Status { get; init; }
}
