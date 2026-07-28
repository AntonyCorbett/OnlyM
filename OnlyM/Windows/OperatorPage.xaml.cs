using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
    private Point _dragStartPoint;
    private MediaItem? _draggedItem;
    private Popup? _dragPopup;
    private TextBlock? _dragPopupText;
    private DispatcherTimer? _dragPopupSafetyTimer;

    public OperatorPage()
    {
        InitializeComponent();

        var dragAndDropService = Ioc.Default.GetService<IDragAndDropService>();
        dragAndDropService?.Init(this);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var vm = (OperatorViewModel?)DataContext;
        vm?.TriggerStartupLoad();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        HideDragPopup();

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

    private void OperatorMediaList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        _dragStartPoint = e.GetPosition(null);

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

        _draggedItem = GetMediaItemFromOriginalSource(e.OriginalSource as DependencyObject);

        if (_draggedItem == null || _draggedItem.IsBlankScreen)
        {
            return;
        }

        try
        {
            _draggedItem.IsBeingDragged = true;
            ShowDragPopup(_draggedItem);
            Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
            Mouse.OverrideCursor = Cursors.SizeAll;
            DragDrop.DoDragDrop((DependencyObject)sender, _draggedItem, DragDropEffects.Move);
        }
        finally
        {
            Mouse.OverrideCursor = null;
            HideDragPopup();

            if (_draggedItem != null)
            {
                _draggedItem.IsBeingDragged = false;
            }
        }
    }

    private void OperatorMediaList_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(MediaItem)))
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.None;
    }

    private void OperatorMediaList_GiveFeedback(object sender, GiveFeedbackEventArgs e)
    {
        UpdateDragPopupPosition();
        e.UseDefaultCursors = false;
        Mouse.SetCursor(Cursors.SizeAll);
        e.Handled = true;
    }

    private void OperatorMediaList_Drop(object sender, DragEventArgs e)
    {
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

    private void ShowDragPopup(MediaItem item)
    {
        HideDragPopup();

        _dragPopupText = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(item.Title) ? "Moving item" : $"Moving: {item.Title}",
            Foreground = Brushes.White,
            Background = Brushes.Black,
            Opacity = 0.88,
            Padding = new Thickness(10, 6, 10, 6),
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 320,
        };

        _dragPopup = new Popup
        {
            AllowsTransparency = true,
            Placement = PlacementMode.Absolute,
            StaysOpen = true,
            IsHitTestVisible = false,
            Child = _dragPopupText,
            IsOpen = true,
        };

        _dragPopupSafetyTimer?.Stop();
        _dragPopupSafetyTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _dragPopupSafetyTimer.Tick += (_, _) =>
        {
            _dragPopupSafetyTimer?.Stop();
            _dragPopupSafetyTimer = null;
            HideDragPopup();
        };
        _dragPopupSafetyTimer.Start();

        UpdateDragPopupPosition();
    }

    private void UpdateDragPopupPosition()
    {
        if (_dragPopup == null || !_dragPopup.IsOpen)
        {
            return;
        }

        var p = System.Windows.Forms.Cursor.Position;
        _dragPopup.HorizontalOffset = p.X + 16;
        _dragPopup.VerticalOffset = p.Y + 20;
    }

    private void HideDragPopup()
    {
        _dragPopupSafetyTimer?.Stop();
        _dragPopupSafetyTimer = null;

        if (_dragPopup != null)
        {
            _dragPopup.IsOpen = false;
            _dragPopup.Child = null;
            _dragPopup = null;
            _dragPopupText = null;
        }
    }
}
