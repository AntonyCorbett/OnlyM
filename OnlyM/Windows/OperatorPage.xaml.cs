using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using OnlyM.Models;
using OnlyM.PubSubMessages;
using OnlyM.Services.DragAndDrop;
using OnlyM.ViewModel;

namespace OnlyM.Windows;

/// <summary>
/// Interaction logic for OperatorPage.xaml
/// </summary>
public partial class OperatorPage
{
    private const double AutoScrollHotZoneHeight = 40;
    private const double AutoScrollMinStep = 0.04;
    private const double AutoScrollMaxStep = 5.0;
    private const double AutoScrollAccelerationDistance = 600;

    private Point _dragStartPoint;
    private MediaItem? _dragStartItem;
    private bool _dragStartOnInteractiveControl;
    private bool _isMediaItemDragInProgress;
    private MediaItem? _draggedItem;
    private DispatcherTimer? _autoScrollTimer;
    private ScrollViewer? _mediaListScrollViewer;
    private ListBoxItem? _insertionAdornerItem;
    private AdornerLayer? _insertionAdornerLayer;
    private InsertionAdorner? _insertionAdorner;

    public OperatorPage()
    {
        InitializeComponent();

        var dragAndDropService = Ioc.Default.GetService<IDragAndDropService>();
        dragAndDropService?.Init(this);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _mediaListScrollViewer = FindDescendant<ScrollViewer>(OperatorMediaList);

        var vm = (OperatorViewModel?)DataContext;
        vm?.TriggerStartupLoad();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        StopAutoScroll();
        HideInsertionAdorner();

        if (_draggedItem != null)
        {
            _draggedItem.IsBeingDragged = false;
            _draggedItem = null;
        }

        Mouse.OverrideCursor = null;
    }

    private void MirrorCheckBoxChecked(object? sender, RoutedEventArgs e) =>
        HandleMirrorCheckBoxChanged(sender, true);

    private void MirrorCheckBoxUnchecked(object? sender, RoutedEventArgs e) =>
        HandleMirrorCheckBoxChanged(sender, false);

    private static void HandleMirrorCheckBoxChanged(object? sender, bool isChecked)
    {
        if (sender is CheckBox cb)
        {
            var mediaItemGuid = (Guid)cb.Tag;
            WeakReferenceMessenger.Default.Send(new MirrorWindowMessage { MediaItemId = mediaItemGuid, UseMirror = isChecked });
        }
    }

    private void OnlyMOperatorPage_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        var vm = (OperatorViewModel?)DataContext;

        if (vm == null)
        {
            return;
        }

        vm.ThumbnailColWidth = e.NewSize.Width switch
        {
            >= 500 => 180,
            >= 400 => 100,
            _ => 46
        };
    }

    private void OperatorMediaList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        _dragStartPoint = e.GetPosition(null);
        _dragStartItem = GetMediaItemFromOriginalSource(source);
        _dragStartOnInteractiveControl = IsDragBlockedSource(source);
    }

    private void OperatorMediaList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var currentPos = e.GetPosition(null);

        if (Math.Abs(currentPos.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(currentPos.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        if (_dragStartOnInteractiveControl)
        {
            return;
        }

        _draggedItem = _dragStartItem;

        if (_draggedItem == null || _draggedItem.IsBlankScreen)
        {
            return;
        }

        try
        {
            _draggedItem.IsBeingDragged = true;
            _isMediaItemDragInProgress = true;
            StartAutoScroll();
            Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
            Mouse.OverrideCursor = Cursors.SizeAll;
            DragDrop.DoDragDrop((DependencyObject)sender, _draggedItem, DragDropEffects.Move);
        }
        finally
        {
            _isMediaItemDragInProgress = false;
            StopAutoScroll();
            HideInsertionAdorner();
            Mouse.OverrideCursor = null;

            if (_draggedItem != null)
            {
                _draggedItem.IsBeingDragged = false;
            }

            _draggedItem = null;
            _dragStartItem = null;
            _dragStartOnInteractiveControl = false;
        }
    }

    private void OperatorMediaList_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(MediaItem)))
        {
            var sourceItem = e.Data.GetData(typeof(MediaItem)) as MediaItem;
            var targetItem = GetMediaItemFromOriginalSource(e.OriginalSource as DependencyObject);

            if (sourceItem == null || sourceItem.IsBlankScreen || targetItem == null || targetItem.IsBlankScreen)
            {
                HideInsertionAdorner();
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            UpdateInsertionAdorner(sourceItem, targetItem);
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
            return;
        }

        HideInsertionAdorner();
        e.Effects = DragDropEffects.None;
    }

    private void OperatorMediaList_GiveFeedback(object sender, GiveFeedbackEventArgs e)
    {
        e.UseDefaultCursors = false;
        Mouse.SetCursor(Cursors.SizeAll);
        e.Handled = true;
    }

    private void OperatorMediaList_DragLeave(object sender, DragEventArgs e) =>
        HideInsertionAdorner();

    private void OperatorMediaList_Drop(object sender, DragEventArgs e)
    {
        HideInsertionAdorner();

        var vm = DataContext as OperatorViewModel;
        if (vm == null)
        {
            return;
        }

        var targetItem = GetMediaItemFromOriginalSource(e.OriginalSource as DependencyObject);

        if (e.Data.GetDataPresent(typeof(MediaItem)))
        {
            var sourceItem = e.Data.GetData(typeof(MediaItem)) as MediaItem;

            if (sourceItem == null || targetItem == null || sourceItem == targetItem || sourceItem.IsBlankScreen || targetItem.IsBlankScreen)
            {
                return;
            }

            if (!vm.IsManualSortMode)
            {
                var result = MessageBox.Show(
                    "Switch to manual sort?",
                    "Sort mode",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                {
                    return;
                }

                vm.PrepareManualSortForDrag();
            }

            vm.MoveMediaItem(sourceItem, targetItem);
            return;
        }

        var targetIndex = targetItem == null
            ? vm.MediaItems.Count
            : vm.MediaItems.IndexOf(targetItem);

        if (targetIndex < 0)
        {
            targetIndex = vm.MediaItems.Count;
        }

        WeakReferenceMessenger.Default.Send(new ExternalDropTargetMessage { TargetIndex = targetIndex });
    }

    private static MediaItem? GetMediaItemFromOriginalSource(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is FrameworkElement { DataContext: MediaItem mediaItem })
            {
                return mediaItem;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    private static bool IsDragBlockedSource(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is Slider || source is Thumb)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private void UpdateInsertionAdorner(MediaItem sourceItem, MediaItem targetItem)
    {
        var sourceIndex = OperatorMediaList.Items.IndexOf(sourceItem);
        var targetIndex = OperatorMediaList.Items.IndexOf(targetItem);

        if (sourceIndex < 0 || targetIndex < 0 || sourceIndex == targetIndex)
        {
            HideInsertionAdorner();
            return;
        }

        var targetContainer = OperatorMediaList.ItemContainerGenerator.ContainerFromItem(targetItem) as ListBoxItem;
        if (targetContainer == null)
        {
            HideInsertionAdorner();
            return;
        }

        var isAfter = sourceIndex < targetIndex;

        if (_insertionAdorner == null || _insertionAdornerItem != targetContainer || _insertionAdorner.IsAfter != isAfter)
        {
            HideInsertionAdorner();

            _insertionAdornerLayer = AdornerLayer.GetAdornerLayer(targetContainer);
            if (_insertionAdornerLayer == null)
            {
                return;
            }

            _insertionAdornerItem = targetContainer;
            _insertionAdorner = new InsertionAdorner(targetContainer, isAfter);
            _insertionAdornerLayer.Add(_insertionAdorner);
        }
    }

    private void HideInsertionAdorner()
    {
        if (_insertionAdorner != null && _insertionAdornerLayer != null)
        {
            _insertionAdornerLayer.Remove(_insertionAdorner);
        }

        _insertionAdorner = null;
        _insertionAdornerLayer = null;
        _insertionAdornerItem = null;
    }

    private void StartAutoScroll()
    {
        _mediaListScrollViewer ??= FindDescendant<ScrollViewer>(OperatorMediaList);

        if (_mediaListScrollViewer == null)
        {
            return;
        }

        _autoScrollTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(35) };
        _autoScrollTimer.Tick -= AutoScrollTimerTick;
        _autoScrollTimer.Tick += AutoScrollTimerTick;
        _autoScrollTimer.Start();
    }

    private void StopAutoScroll()
    {
        if (_autoScrollTimer != null)
        {
            _autoScrollTimer.Stop();
            _autoScrollTimer.Tick -= AutoScrollTimerTick;
            _autoScrollTimer = null;
        }
    }

    private void AutoScrollTimerTick(object? sender, EventArgs e)
    {
        if (!_isMediaItemDragInProgress || _mediaListScrollViewer == null)
        {
            return;
        }

        var height = OperatorMediaList.ActualHeight;
        if (height <= 0)
        {
            return;
        }

        var cursorScreenPos = System.Windows.Forms.Cursor.Position;
        var cursorPos = OperatorMediaList.PointFromScreen(new Point(cursorScreenPos.X, cursorScreenPos.Y));

        var delta = 0d;

        if (cursorPos.Y < AutoScrollHotZoneHeight)
        {
            delta = -GetAutoScrollStep(AutoScrollHotZoneHeight - cursorPos.Y);
        }
        else if (cursorPos.Y > height - AutoScrollHotZoneHeight)
        {
            delta = GetAutoScrollStep(cursorPos.Y - (height - AutoScrollHotZoneHeight));
        }

        if (Math.Abs(delta) < double.Epsilon)
        {
            return;
        }

        var offset = _mediaListScrollViewer.VerticalOffset + delta;
        offset = Math.Max(0, Math.Min(_mediaListScrollViewer.ScrollableHeight, offset));
        _mediaListScrollViewer.ScrollToVerticalOffset(offset);
    }

    private static double GetAutoScrollStep(double edgeDistance)
    {
        var ratio = Math.Clamp(edgeDistance / AutoScrollAccelerationDistance, 0, 1);
        return AutoScrollMinStep + ((AutoScrollMaxStep - AutoScrollMinStep) * ratio);
    }

    private static T? FindDescendant<T>(DependencyObject parent)
        where T : DependencyObject
    {
        var childCount = VisualTreeHelper.GetChildrenCount(parent);

        for (var n = 0; n < childCount; ++n)
        {
            var child = VisualTreeHelper.GetChild(parent, n);
            if (child is T typedChild)
            {
                return typedChild;
            }

            var descendant = FindDescendant<T>(child);
            if (descendant != null)
            {
                return descendant;
            }
        }

        return null;
    }

    private sealed class InsertionAdorner(UIElement adornedElement, bool isAfter) : Adorner(adornedElement)
    {
        private static readonly Pen InsertionPen = new(Brushes.OrangeRed, 2);

        public bool IsAfter { get; } = isAfter;

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            var y = IsAfter ? AdornedElement.RenderSize.Height : 0;
            var start = new Point(0, y);
            var end = new Point(AdornedElement.RenderSize.Width, y);
            drawingContext.DrawLine(InsertionPen, start, end);
        }
    }
}
