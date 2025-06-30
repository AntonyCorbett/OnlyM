using System;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OnlyMSlideManager.Models;

public sealed class SlideItem : ObservableObject
{
    private bool _showCardBorder;
    private bool _fadeInForward;
    private bool _fadeOutForward;
    private bool _fadeInReverse;
    private bool _fadeOutReverse;
    private int? _dwellTimeSeconds;

    public event EventHandler? SlideItemModifiedEvent;

    public string? Name { get; init; }

    public bool IsEndMarker { get; init; }

    public string? OriginalFilePath { get; init; }

    public ImageSource? ThumbnailImage { get; init; }

    public int SlideIndex { get; set; }

    public bool FadeInForward
    {
        get => _fadeInForward;
        set
        {
            if (SetProperty(ref _fadeInForward, value))
            {
                OnSlideItemModifiedEvent();
            }
        }
    }

    public bool FadeInReverse
    {
        get => _fadeInReverse;
        set
        {
            if (SetProperty(ref _fadeInReverse, value))
            {
                OnSlideItemModifiedEvent();
            }
        }
    }

    public bool FadeOutForward
    {
        get => _fadeOutForward;
        set
        {
            if (SetProperty(ref _fadeOutForward, value))
            {
                OnSlideItemModifiedEvent();
            }
        }
    }

    public bool FadeOutReverse
    {
        get => _fadeOutReverse;
        set
        {
            if (SetProperty(ref _fadeOutReverse, value))
            {
                OnSlideItemModifiedEvent();
            }
        }
    }

    public int? DwellTimeSeconds
    {
        get => _dwellTimeSeconds;
        set
        {
            if (SetProperty(ref _dwellTimeSeconds, value))
            {
                OnSlideItemModifiedEvent();
            }
        }
    }

    public bool ShowCardBorder
    {
        get => _showCardBorder;
        set
        {
            if (SetProperty(ref _showCardBorder, value))
            {
                OnPropertyChanged();
            }
        }
    }

    public string? DropZoneId { get; init; }

    private void OnSlideItemModifiedEvent() =>
        SlideItemModifiedEvent?.Invoke(this, EventArgs.Empty);
}
