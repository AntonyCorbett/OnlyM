using System;
using System.Windows;
using CefSharp.Wpf;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OnlyM.Core.Extensions;
using OnlyM.Core.Models;
using OnlyM.Core.Services.Options;
using OnlyM.CustomControls.MagnifierControl;

namespace OnlyM.ViewModel;

// ReSharper disable once ClassNeverInstantiated.Global
internal sealed class MediaViewModel : ObservableObject
{
    private readonly IOptionsService _optionsService;
    private string? _subtitleText;
    private IWpfChromiumWebBrowser? _webBrowser;
    private bool _isMagnifierVisible;
    private Size _windowSize;
    private bool _isWebPage;
    private int _videoRotation;

    public MediaViewModel(IOptionsService optionsService)
    {
        _optionsService = optionsService;

        _optionsService.RenderingMethodChangedEvent += HandleRenderingMethodChangedEvent;
        _optionsService.MagnifierChangedEvent += HandleMagnifierChangedEvent;
        _optionsService.BrowserChangedEvent += HandleBrowserChangedEvent;

        InitCommands();
    }

    public int VideoRotation
    {
        get => _videoRotation;
        set => SetProperty(ref _videoRotation, value);
    }

    public bool EngineIsMediaFoundation => _optionsService.RenderingMethod == RenderingMethod.MediaFoundation;

    public bool EngineIsFfmpeg => _optionsService.RenderingMethod == RenderingMethod.Ffmpeg;

    public RelayCommand ToggleMagnifier { get; set; } = null!;

    public RelayCommand ToggleMagnifierFrame { get; set; } = null!;

    public RelayCommand MagnifierSmaller { get; set; } = null!;

    public RelayCommand MagnifierLarger { get; set; } = null!;

    public IWpfChromiumWebBrowser? WebBrowser
    {
        get => _webBrowser;
        set => SetProperty(ref _webBrowser, value);
    }

    public Size WindowSize
    {
        get => _windowSize;
        set
        {
            if (SetProperty(ref _windowSize, value))
            {
                OnPropertyChanged(nameof(MagnifierRadius));
            }
        }
    }

    public double BrowserZoomLevelIncrement
    {
        get => _optionsService.BrowserZoomLevelIncrement;
        set
        {
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (_optionsService.BrowserZoomLevelIncrement != value)
            {
                _optionsService.BrowserZoomLevelIncrement = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsWebPage
    {
        get => _isWebPage;
        set => SetProperty(ref _isWebPage, value);
    }

    public bool IsMagnifierVisible
    {
        get => _isMagnifierVisible;
        set
        {
            SetProperty(ref _isMagnifierVisible, value);
            OnPropertyChanged(nameof(MagnifierDescription));
        }
    }

    public double MagnifierZoomLevel
    {
        get => _optionsService.MagnifierZoomLevel;
        set
        {
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (_optionsService.MagnifierZoomLevel != value)
            {
                _optionsService.MagnifierZoomLevel = value;
                OnPropertyChanged();
            }
        }
    }

    public string? SubTitleText
    {
        get => _subtitleText;
        set
        {
            if (SetProperty(ref _subtitleText, value))
            {
                OnPropertyChanged(nameof(SubTitleTextIsNotEmpty));
            }
        }
    }

    public double MagnifierFrameThickness => _optionsService.MagnifierFrameThickness;

    public double MagnifierRadius
    {
        get
        {
            var r = CalculateMagnifierRadius();
            return r;
        }
    }

    public bool IsMagnifierFrameSquare
    {
        get => _optionsService.MagnifierShape == MagnifierShape.Square;
        set
        {
            if (_optionsService.MagnifierShape == MagnifierShape.Square != value)
            {
                MagnifierShape = value
                    ? MagnifierShape.Square
                    : MagnifierShape.Circle;
            }
        }
    }

    public FrameType MagnifierFrameType
    {
        get
        {
            switch (_optionsService.MagnifierShape)
            {
                default:
                    return FrameType.Circle;

                case MagnifierShape.Square:
                    return FrameType.Rectangle;
            }
        }
    }

    public bool SubTitleTextIsNotEmpty => !string.IsNullOrEmpty(SubTitleText);

    public string MagnifierDescription
    {
        get
        {
            var onOffText = IsMagnifierVisible ? Properties.Resources.WEB_MAGNIFIER_ON : Properties.Resources.WEB_MAGNIFIER_OFF;
            var shapeText = MagnifierShape.GetDescriptiveName();
            var sizeText = MagnifierSize.GetDescriptiveName();

            return $"{Properties.Resources.WEB_MAGNIFIER}: {onOffText}, {shapeText} - {sizeText}";
        }
    }

    private MagnifierSize MagnifierSize
    {
        get => _optionsService.MagnifierSize;
        set
        {
            if (_optionsService.MagnifierSize != value)
            {
                _optionsService.MagnifierSize = value;
                OnPropertyChanged();
                
                OnPropertyChanged(nameof(MagnifierRadius));
                OnPropertyChanged(nameof(MagnifierDescription));
                MagnifierLarger.NotifyCanExecuteChanged();
                MagnifierSmaller.NotifyCanExecuteChanged();
            }
        }
    }

    private MagnifierShape MagnifierShape
    {
        get => _optionsService.MagnifierShape;
        set
        {
            if (_optionsService.MagnifierShape != value)
            {
                _optionsService.MagnifierShape = value;

                OnPropertyChanged();
                OnPropertyChanged(nameof(IsMagnifierFrameSquare));
                OnPropertyChanged(nameof(MagnifierDescription));
            }
        }
    }

    private void HandleRenderingMethodChangedEvent(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(EngineIsFfmpeg));
        OnPropertyChanged(nameof(EngineIsMediaFoundation));
    }

    private void InitCommands()
    {
        ToggleMagnifier = new RelayCommand(DoToggleMagnifier);
        ToggleMagnifierFrame = new RelayCommand(DoToggleMagnifierFrame);
        MagnifierSmaller = new RelayCommand(DoMagnifierSmaller, CanExecuteDoMagnifierSmaller);
        MagnifierLarger = new RelayCommand(DoMagnifierLarger, CanExecuteDoMagnifierLarger);
    }

    private bool CanExecuteDoMagnifierSmaller() => MagnifierSize != MagnifierSize.XXSmall;

    private bool CanExecuteDoMagnifierLarger() => MagnifierSize != MagnifierSize.XXLarge;

    private void DoMagnifierLarger()
    {
        switch (MagnifierSize)
        {
            case MagnifierSize.XXSmall:
                MagnifierSize = MagnifierSize.XSmall;
                break;

            case MagnifierSize.XSmall:
                MagnifierSize =MagnifierSize.Small;
                break;

            case MagnifierSize.Small:
                MagnifierSize = MagnifierSize.Medium;
                break;

            case MagnifierSize.Medium:
                MagnifierSize = MagnifierSize.Large;
                break;

            case MagnifierSize.Large:
                MagnifierSize = MagnifierSize.XLarge;
                break;

            case MagnifierSize.XLarge:
                MagnifierSize = MagnifierSize.XXLarge;
                break;
        }
    }

    private void DoMagnifierSmaller()
    {
        switch (MagnifierSize)
        {
            case MagnifierSize.XXLarge:
                MagnifierSize = MagnifierSize.XLarge;
                break;

            case MagnifierSize.XLarge:
                MagnifierSize = MagnifierSize.Large;
                break;

            case MagnifierSize.Large:
                MagnifierSize = MagnifierSize.Medium;
                break;

            case MagnifierSize.Medium:
                MagnifierSize = MagnifierSize.Small;
                break;

            case MagnifierSize.Small:
                MagnifierSize = MagnifierSize.XSmall;
                break;

            case MagnifierSize.XSmall:
                MagnifierSize = MagnifierSize.XXSmall;
                break;
        }
    }

    private void DoToggleMagnifierFrame()
    {
        switch (MagnifierShape)
        {
            case MagnifierShape.Circle:
                MagnifierShape = MagnifierShape.Square;
                break;

            case MagnifierShape.Square:
                MagnifierShape = MagnifierShape.Circle;
                break;
        }
    }

    private void DoToggleMagnifier() =>
        IsMagnifierVisible = !IsMagnifierVisible;

    private double CalculateMagnifierRadius()
    {
        const int minDelta = 10;
        var delta = Math.Max(WindowSize.Height / 40, minDelta);

        switch (MagnifierSize)
        {
            default:
            case MagnifierSize.Medium:
                return delta * 6;

            case MagnifierSize.XXSmall:
                return delta;

            case MagnifierSize.XSmall:
                return delta * 2;

            case MagnifierSize.Small:
                return delta * 4;

            case MagnifierSize.Large:
                return delta * 8;

            case MagnifierSize.XLarge:
                return delta * 12;

            case MagnifierSize.XXLarge:
                return delta * 18;
        }
    }

    private void HandleMagnifierChangedEvent(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(IsMagnifierVisible));
        OnPropertyChanged(nameof(IsMagnifierFrameSquare));
        OnPropertyChanged(nameof(MagnifierFrameType));
        OnPropertyChanged(nameof(MagnifierZoomLevel));
        OnPropertyChanged(nameof(MagnifierRadius));
        OnPropertyChanged(nameof(MagnifierFrameThickness));
    }

    private void HandleBrowserChangedEvent(object? sender, EventArgs e) =>
        OnPropertyChanged(nameof(BrowserZoomLevelIncrement));
}
