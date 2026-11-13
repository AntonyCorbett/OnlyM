using System;
using System.Windows;
using OnlyM.Models;

namespace OnlyM.Services.DragAndDrop;

internal interface IDragAndDropService
{
    event EventHandler<FilesCopyProgressEventArgs> CopyingFilesProgressEvent;

    void Init(FrameworkElement targetElement);

    void Paste();

    /// <summary>
    /// Copies the specified files into the media folder (as if they had been dropped onto the media list).
    /// </summary>
    void AddFiles(string[] files);

    void Drop(IDataObject data, int targetIndex, string? targetFilePath);
}
