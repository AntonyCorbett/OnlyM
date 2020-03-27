namespace OnlyMSlideManager.Services.DragAndDrop
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Shapes;
    using GalaSoft.MvvmLight.Messaging;
    using OnlyM.CoreSys.Services.UI;
    using OnlyMSlideManager.Models;
    using OnlyMSlideManager.PubSubMessages;

    internal class DragAndDropServiceCustom : IDragAndDropServiceCustom
    {
        private readonly string[] _supportedImageExtensions =
        {
            ".bmp",
            ".png",
            ".jpg",
            ".jpeg",
        };

        private readonly IUserInterfaceService _userInterfaceService;
        private Control _dragSourceCard;
        private Point _startDragPoint;
        private bool _isDragging;

        public DragAndDropServiceCustom(IUserInterfaceService userInterfaceService)
        {
            _userInterfaceService = userInterfaceService;
        }

        public void DragSourcePreviewMouseDown(Control card, Point position)
        {
            if (card != null)
            {
                _dragSourceCard = card;
                _startDragPoint = position;
            }
        }

        public void DragSourcePreviewMouseMove(Point position)
        {
            if (_userInterfaceService.IsBusy())
            {
                return;
            }

            if (!_isDragging && (Math.Abs(position.X - _startDragPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                                 Math.Abs(position.Y - _startDragPoint.Y) > SystemParameters.MinimumVerticalDragDistance))
            {
                StartDrag();
            }
        }

        public void DragEnter(Rectangle rect, DragEventArgs e)
        {
            SetEffects(e, CanDropOrPaste(e.Data));
            e.Handled = true;
        }

        public void Drop(Rectangle rect, DragEventArgs e)
        {
            if (_userInterfaceService.IsBusy())
            {
                return;
            }

            // targetCardViewModel represents the card to the right of the drop zone.
            if (rect.DataContext is SlideItem targetCardViewModel)
            {
                if (_dragSourceCard?.DataContext is SlideItem sourceCardViewModel)
                {
                    Messenger.Default.Send(new ReorderMessage
                    {
                        SourceItem = sourceCardViewModel,
                        TargetId = targetCardViewModel.DropZoneId,
                    });
                }
                else
                {
                    // The drag object is from another application...
                    if (e.Data != null)
                    {
                        HandleDropExternalImage(e.Data, targetCardViewModel);
                    }
                }
            }
        }

        private void HandleDropExternalImage(IDataObject data, SlideItem targetCardViewModel)
        {
            var files = GetSupportedFiles(data).ToList();
            files.Sort();

            Messenger.Default.Send(new DropImagesMessage
            {
                FileList = files,
                TargetId = targetCardViewModel.DropZoneId,
            });
        }

        private void StartDrag()
        {
            if (_dragSourceCard?.DataContext is SlideItem cardViewModel)
            {
                _isDragging = true;
                cardViewModel.ShowCardBorder = true;

                var objectToDrag = new SourceCard
                {
                    Name = cardViewModel.Name,
                };

                var data = new DataObject(DataFormats.Serializable, objectToDrag);

                DragDrop.DoDragDrop(_dragSourceCard, data, DragDropEffects.Move);

                cardViewModel.ShowCardBorder = false;
                _dragSourceCard = null;
                _isDragging = false;
            }
        }

        private bool CanDropOrPaste(IDataObject data)
        {
            return GetSupportedFiles(data).Any();
        }

        private IEnumerable<string> GetSupportedFiles(IDataObject data)
        {
            if (data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file...
                string[] files = (string[])data.GetData(DataFormats.FileDrop);

                if (files != null && files.Any())
                {
                    foreach (var file in files)
                    {
                        if (Directory.Exists(file))
                        {
                            // a folder rather than a file.
                            foreach (var fileInFolder in Directory.EnumerateFiles(file))
                            {
                                var fileToAdd = GetSupportedFile(fileInFolder);
                                if (fileToAdd != null)
                                {
                                    yield return fileToAdd;
                                }
                            }
                        }
                        else
                        {
                            var fileToAdd = GetSupportedFile(file);
                            if (fileToAdd != null)
                            {
                                yield return fileToAdd;
                            }
                        }
                    }
                }
            }
        }

        private string GetSupportedFile(string file)
        {
            var ext = System.IO.Path.GetExtension(file);
            if (string.IsNullOrEmpty(ext) || !IsFileExtensionSupported(ext))
            {
                return null;
            }

            return file;
        }

        private bool IsFileExtensionSupported(string ext)
        {
            return _supportedImageExtensions.Contains(ext.ToLower());
        }

        private void SetEffects(DragEventArgs e, bool canDrop)
        {
            e.Effects = canDrop
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        }
    }
}
