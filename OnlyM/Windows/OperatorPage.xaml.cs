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
    private const double AutoScrollMinStep = 2;
    private const double AutoScrollMaxStep = 20;

    private Point _dragStartPoint;
    private MediaItem? _dragStartItem;
    private bool _dragStartOnInteractiveControl;
    private bool _isMediaItemDragInProgress;
    private bool _isExternalFileDragInProgress;
    private MediaItem? _draggedItem;
    private DispatcherTimer? _autoScrollTimer;
    private ScrollViewer? _mediaListScrollViewer;
    private AdornerLayer? _insertionAdornerLayer;
    private InsertionAdorner? _insertionAdorner;

    public OperatorPage()
    {
        InitializeComponent();

        var dragAndDropService = Ioc.Default.GetService<IDragAndDropService>();
        dragAndDropService?.Init(this);
    }

    internal static MediaItem? GetMediaItemFromOriginalSource(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is FrameworkElement { DataContext: MediaItem mediaItem })
            {
                return mediaItem;
            }

            source = GetInputParent(source);
        }

        return null;
    }

    internal static bool IsDragBlockedSource(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is Slider || source is Thumb || source is ButtonBase || source is ComboBox || source is TextBoxBase)
            {
                return true;
            }

            source = GetInputParent(source);
        }

        return false;
    }

    private static DependencyObject? GetInputParent(DependencyObject source) => source switch
    {
        FrameworkContentElement content => content.Parent,
        Visual or System.Windows.Media.Media3D.Visual3D => VisualTreeHelper.GetParent(source),
        _ => LogicalTreeHelper.GetParent(source),
    };

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _mediaListScrollViewer = FindDescendant<ScrollViewer>(OperatorMediaList);

        if (DataContext is OperatorViewModel vm)
        {
            vm.TriggerStartupLoad();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        StopAutoScroll();
        RemoveInsertionAdorner();

        _draggedItem?.IsBeingDragged = false;
        _draggedItem = null;
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
        _dragStartItem = null;
        _dragStartOnInteractiveControl = false;
        if (DataContext is not OperatorViewModel { IsManualSortMode: true })
        {
            return;
        }

        var source = e.OriginalSource as DependencyObject;
        _dragStartPoint = e.GetPosition(null);
        _dragStartItem = GetMediaItemFromOriginalSource(source);
        _dragStartOnInteractiveControl = IsDragBlockedSource(source);
    }

    private void OperatorMediaList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (DataContext is not OperatorViewModel { IsManualSortMode: true } ||
            e.LeftButton != MouseButtonState.Pressed)
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
            DragDrop.DoDragDrop((DependencyObject)sender, _draggedItem, DragDropEffects.Move);
        }
        finally
        {
            _isMediaItemDragInProgress = false;
            StopAutoScroll();
            RemoveInsertionAdorner();

            _draggedItem?.IsBeingDragged = false;
            _draggedItem = null;
            _dragStartItem = null;
            _dragStartOnInteractiveControl = false;
        }
    }

    private void OperatorMediaList_DragOver(object sender, DragEventArgs e)
    {
        // Handle both PreviewDragEnter and PreviewDragOver so child transitions
        // cannot route an internal move to the page's external file-drop handler.
        if (e.Data.GetDataPresent(typeof(MediaItem)))
        {
            var targetItem = GetMediaItemUnderPointer(e);

            if (DataContext is not OperatorViewModel { IsManualSortMode: true } ||
                e.Data.GetData(typeof(MediaItem)) is not MediaItem sourceItem || sourceItem.IsBlankScreen ||
                targetItem == null || targetItem.IsBlankScreen ||
                sourceItem == targetItem ||
                !OperatorMediaList.Items.Contains(sourceItem) || !OperatorMediaList.Items.Contains(targetItem))
            {
                HideInsertionAdornerLine();
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            UpdateInsertionAdorner(sourceItem, targetItem);
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
            return;
        }

        if (DataContext is OperatorViewModel { IsManualSortMode: true } &&
            e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            _isExternalFileDragInProgress = true;
            StartAutoScroll();
            UpdateExternalInsertionAdorner(e.GetPosition(OperatorMediaList));
        }
        else
        {
            StopAutoScroll();
            HideInsertionAdornerLine();
        }

        // Leave external drag effects and acceptance to the page's file-drop handler.
        e.Effects = DragDropEffects.None;
    }

    private void OperatorMediaList_DragLeave(object sender, DragEventArgs e)
    {
        // DragLeave also fires when the hit-tested child changes within the list.
        // Keep the insertion line until the pointer actually leaves the list;
        // DragEnter/DragOver will update it for the next child.
        if (!new Rect(OperatorMediaList.RenderSize).Contains(e.GetPosition(OperatorMediaList)))
        {
            if (_isExternalFileDragInProgress)
            {
                StopAutoScroll();
            }

            HideInsertionAdornerLine();
        }
    }

    private void OperatorMediaList_Drop(object sender, DragEventArgs e)
    {
        StopAutoScroll();
        HideInsertionAdornerLine();

        if (DataContext is not OperatorViewModel vm)
        {
            return;
        }

        var targetItem = GetMediaItemUnderPointer(e);

        if (e.Data.GetDataPresent(typeof(MediaItem)))
        {
            e.Handled = true;
            if (e.Data.GetData(typeof(MediaItem)) is not MediaItem sourceItem || sourceItem.IsBlankScreen)
            {
                return;
            }

            if (targetItem == null || sourceItem == targetItem || targetItem.IsBlankScreen)
            {
                return;
            }

            vm.MoveMediaItem(sourceItem, targetItem);
            return;
        }

        if (!vm.IsManualSortMode)
        {
            return;
        }

        var targetIndex = targetItem == null
            ? vm.MediaItems.Count
            : vm.MediaItems.IndexOf(targetItem);

        if (targetIndex < 0)
        {
            targetIndex = vm.MediaItems.Count;
        }

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var (item, isAfter) = GetExternalDropTarget(e.GetPosition(OperatorMediaList));
            targetIndex = item == null ? vm.MediaItems.Count : vm.MediaItems.IndexOf(item);
            if (item != null && isAfter)
            {
                ++targetIndex;
            }

            // The copy operation anchors insertion before the next item; null appends.
            targetItem = targetIndex >= 0 && targetIndex < vm.MediaItems.Count
                ? vm.MediaItems[targetIndex]
                : null;
        }

        if (Ioc.Default.GetService<IDragAndDropService>() is IDragAndDropService dragAndDropService)
        {
            e.Handled = true;
            dragAndDropService.Drop(e.Data, targetIndex, targetItem?.FilePath);
        }
    }

    private (MediaItem? Item, bool IsAfter) GetExternalDropTarget(Point position)
    {
        var hit = VisualTreeHelper.HitTest(OperatorMediaList, position)?.VisualHit;
        var item = GetMediaItemFromOriginalSource(hit);
        if (item == null)
        {
            return (OperatorMediaList.Items.Count > 0 && OperatorMediaList.Items[^1] is MediaItem last
                ? last
                : null, true);
        }

        var isAfter = item.IsBlankScreen;
        if (OperatorMediaList.ItemContainerGenerator.ContainerFromItem(item) is UIElement container)
        {
            var bounds = container.TransformToAncestor(OperatorMediaList)
                .TransformBounds(new Rect(container.RenderSize));
            isAfter |= position.Y >= bounds.Top + (bounds.Height / 2);
        }

        return (item, isAfter);
    }

    private void UpdateExternalInsertionAdorner(Point position)
    {
        var (item, isAfter) = GetExternalDropTarget(position);
        if (item != null)
        {
            UpdateInsertionAdorner(item, isAfter);
        }
        else
        {
            HideInsertionAdornerLine();
        }
    }

    private MediaItem? GetMediaItemUnderPointer(DragEventArgs e)
    {
        // e.OriginalSource is unreliable during a native OS drag (it can intermittently
        // fail to resolve to the hovered element even without the pointer moving), which
        // caused the insertion adorner and drop-cursor to flicker. An explicit hit test
        // against the current pointer position is deterministic.
        var hit = VisualTreeHelper.HitTest(OperatorMediaList, e.GetPosition(OperatorMediaList))?.VisualHit;
        return GetMediaItemFromOriginalSource(hit);
    }

    private void UpdateInsertionAdorner(MediaItem sourceItem, MediaItem targetItem)
    {
        var sourceIndex = OperatorMediaList.Items.IndexOf(sourceItem);
        var targetIndex = OperatorMediaList.Items.IndexOf(targetItem);

        if (sourceIndex < 0 || targetIndex < 0 || sourceIndex == targetIndex)
        {
            HideInsertionAdornerLine();
            return;
        }

        UpdateInsertionAdorner(targetItem, sourceIndex < targetIndex);
    }

    private void UpdateInsertionAdorner(MediaItem targetItem, bool isAfter)
    {
        if (OperatorMediaList.ItemContainerGenerator.ContainerFromItem(targetItem) is not UIElement targetContainer)
        {
            HideInsertionAdornerLine();
            return;
        }

        // The adorner is attached once to the whole list (see EnsureInsertionAdorner) and just
        // repositioned from here on. Adding/removing a new Adorner to the AdornerLayer on every
        // DragOver call - which fires very frequently during a drag - caused visible flicker.
        var bounds = targetContainer
            .TransformToAncestor(OperatorMediaList)
            .TransformBounds(new Rect(targetContainer.RenderSize));

        // Match the card's rendered horizontal edges, excluding its outer margin,
        // while keeping the insertion line in the gap between item containers.
        var card = FindDescendant<MaterialDesignThemes.Wpf.Card>(targetContainer);
        if (card == null)
        {
            HideInsertionAdornerLine();
            return;
        }

        var cardBounds = card
            .TransformToAncestor(OperatorMediaList)
            .TransformBounds(new Rect(card.RenderSize));
        bounds = new Rect(cardBounds.Left, bounds.Top, cardBounds.Width, bounds.Height);

        EnsureInsertionAdorner();
        _insertionAdorner?.Show(bounds, isAfter);
    }

    private void EnsureInsertionAdorner()
    {
        if (_insertionAdorner != null)
        {
            return;
        }

        _insertionAdornerLayer = AdornerLayer.GetAdornerLayer(OperatorMediaList);
        if (_insertionAdornerLayer == null)
        {
            return;
        }

        _insertionAdorner = new InsertionAdorner(OperatorMediaList) { IsHitTestVisible = false };
        _insertionAdornerLayer.Add(_insertionAdorner);
    }

    private void HideInsertionAdornerLine() => _insertionAdorner?.Hide();

    private void RemoveInsertionAdorner()
    {
        if (_insertionAdorner != null && _insertionAdornerLayer != null)
        {
            _insertionAdornerLayer.Remove(_insertionAdorner);
        }

        _insertionAdorner = null;
        _insertionAdornerLayer = null;
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
        _isExternalFileDragInProgress = false;
        if (_autoScrollTimer != null)
        {
            _autoScrollTimer.Stop();
            _autoScrollTimer.Tick -= AutoScrollTimerTick;
            _autoScrollTimer = null;
        }
    }

    private void AutoScrollTimerTick(object? sender, EventArgs e)
    {
        if ((!_isMediaItemDragInProgress && !_isExternalFileDragInProgress) || _mediaListScrollViewer == null)
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
        if (_isExternalFileDragInProgress)
        {
            if (!new Rect(OperatorMediaList.RenderSize).Contains(cursorPos))
            {
                StopAutoScroll();
                HideInsertionAdornerLine();
                return;
            }

            UpdateExternalInsertionAdorner(cursorPos);
        }

        if (cursorPos.X < 0 || cursorPos.X > OperatorMediaList.ActualWidth)
        {
            return;
        }

        var delta = 0d;
        var hotZoneHeight = Math.Min(AutoScrollHotZoneHeight, height / 2);

        if (cursorPos.Y < hotZoneHeight)
        {
            delta = -GetAutoScrollStep(hotZoneHeight - cursorPos.Y, hotZoneHeight);
        }
        else if (cursorPos.Y > height - hotZoneHeight)
        {
            delta = GetAutoScrollStep(cursorPos.Y - (height - hotZoneHeight), hotZoneHeight);
        }

        if (Math.Abs(delta) < double.Epsilon)
        {
            return;
        }

        var offset = _mediaListScrollViewer.VerticalOffset + delta;
        offset = Math.Max(0, Math.Min(_mediaListScrollViewer.ScrollableHeight, offset));
        _mediaListScrollViewer.ScrollToVerticalOffset(offset);
    }

    private static double GetAutoScrollStep(double edgeDistance, double hotZoneHeight)
    {
        var ratio = Math.Clamp(edgeDistance / hotZoneHeight, 0, 1);
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

    private sealed class InsertionAdorner(UIElement adornedElement) : Adorner(adornedElement)
    {
        private static readonly Pen InsertionPen = new(Brushes.OrangeRed, 2);

        private Rect? _lineBounds;
        private bool _isAfter;

        public void Show(Rect targetBounds, bool isAfter)
        {
            if (_lineBounds == targetBounds && _isAfter == isAfter)
            {
                return;
            }

            _lineBounds = targetBounds;
            _isAfter = isAfter;
            InvalidateVisual();
        }

        public void Hide()
        {
            if (_lineBounds == null)
            {
                return;
            }

            _lineBounds = null;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            if (_lineBounds is not { } bounds)
            {
                return;
            }

            var y = _isAfter ? bounds.Bottom : bounds.Top;
            var height = AdornedElement.RenderSize.Height;
            if (y < 0 || y > height || height < InsertionPen.Thickness)
            {
                return;
            }

            // The stroke is centered on y. Keep its full thickness inside the
            // list when the insertion boundary is at the top or bottom edge.
            var halfThickness = InsertionPen.Thickness / 2;
            y = Math.Clamp(y, halfThickness, height - halfThickness);
            drawingContext.DrawLine(InsertionPen, new Point(bounds.Left, y), new Point(bounds.Right, y));
        }
    }
}
