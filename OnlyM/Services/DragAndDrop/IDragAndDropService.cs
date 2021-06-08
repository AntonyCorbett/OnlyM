using System;
using System.Windows;
using OnlyM.Models;

namespace OnlyM.Services.DragAndDrop
{
    internal interface IDragAndDropService
    {
        event EventHandler<FilesCopyProgressEventArgs> CopyingFilesProgressEvent;

        void Init(FrameworkElement targetElement);

        void Paste();
    }
}
