namespace OnlyMSlideManager.Services.DragAndDrop
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Shapes;
    using GalaSoft.MvvmLight.Messaging;
    using MaterialDesignThemes.Wpf;
    using OnlyMSlideManager.Models;
    using OnlyMSlideManager.PubSubMessages;

    internal class DragAndDropServiceCustom : IDragAndDropServiceCustom
    {
        private Control _dragSourceCard;
        private Point _startDragPoint;
        private bool _isDragging;

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
            if (!_isDragging)
            {
                if (Math.Abs(position.X - _startDragPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(position.Y - _startDragPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    StartDrag(position);
                }
            }
        }

        public void Drop(Rectangle rect)
        {
            if (rect.DataContext is SlideItem targetCardViewModel)
            {
                // targetCardViewModel represents the card to the right of the drop zone.
                if (_dragSourceCard?.DataContext is SlideItem sourceCardViewModel)
                {
                    Messenger.Default.Send(new ReorderMessage
                    {
                        SourceItem = sourceCardViewModel,
                        TargetId = targetCardViewModel.DropZoneId,
                    });
                }
            }
        }

        private void StartDrag(Point position)
        {
            if (_dragSourceCard?.DataContext is SlideItem cardViewModel)
            {
                _isDragging = true;
                cardViewModel.ShowCardBorder = true;

                var objectToDrag = new SourceCard
                {
                    Name = cardViewModel.Name
                };

                var data = new DataObject(DataFormats.Serializable, objectToDrag);

                DragDrop.DoDragDrop(_dragSourceCard, data, DragDropEffects.Move);

                cardViewModel.ShowCardBorder = false;
                _dragSourceCard = null;
                _isDragging = false;
            }
        }
    }
}
