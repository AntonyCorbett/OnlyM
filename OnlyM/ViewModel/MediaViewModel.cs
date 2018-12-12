namespace OnlyM.ViewModel
{
    using System;
    using System.Windows;
    using CefSharp.Wpf;
    using Core.Models;
    using Core.Services.Options;
    using GalaSoft.MvvmLight;
    using GalaSoft.MvvmLight.CommandWpf;
    using OnlyM.CustomControls.MagnifierControl;

    // ReSharper disable once ClassNeverInstantiated.Global
    internal class MediaViewModel : ViewModelBase
    {
        private readonly IOptionsService _optionsService;
        private string _subtitleText;
        private IWpfWebBrowser _webBrowser;
        private bool _isMagnifierVisible;
        private Size _windowSize;
        private bool _isWebPage;
        
        public MediaViewModel(IOptionsService optionsService)
        {
            _optionsService = optionsService;
            
            _optionsService.RenderingMethodChangedEvent += HandleRenderingMethodChangedEvent;
            _optionsService.MagnifierChangedEvent += HandleMagnifierChangedEvent;
            _optionsService.BrowserChangedEvent += HandleBrowserChangedEvent;

            InitCommands();
        }

        public bool EngineIsMediaFoundation => _optionsService.RenderingMethod == RenderingMethod.MediaFoundation;

        public bool EngineIsFfmpeg => _optionsService.RenderingMethod == RenderingMethod.Ffmpeg;

        public RelayCommand ToggleMagnifier { get; set; }

        public RelayCommand ToggleMagnifierFrame { get; set; }

        public RelayCommand MagnifierSmaller { get; set; }

        public RelayCommand MagnifierLarger { get; set; }

        public IWpfWebBrowser WebBrowser
        {
            get => _webBrowser;
            set => Set(ref _webBrowser, value);
        }

        public Size WindowSize
        {
            get => _windowSize;
            set
            {
                if (_windowSize != value)
                {
                    _windowSize = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(MagnifierRadius));
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
                    RaisePropertyChanged();
                }
            }
        }

        public bool IsWebPage
        {
            get => _isWebPage;
            set
            {
                if (_isWebPage != value)
                {
                    _isWebPage = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool IsMagnifierVisible
        {
            get => _isMagnifierVisible;
            set
            {
                if (_isMagnifierVisible != value)
                {
                    _isMagnifierVisible = value;
                    RaisePropertyChanged();
                }
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
                    RaisePropertyChanged();
                }
            }
        }

        public string SubTitleText
        {
            get => _subtitleText;
            set
            {
                if (_subtitleText != value)
                {
                    _subtitleText = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(SubTitleTextIsNotEmpty));
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
                    case MagnifierShape.Circle:
                        return FrameType.Circle;

                    case MagnifierShape.Square:
                        return FrameType.Rectangle;
                }
            }
        }

        public bool SubTitleTextIsNotEmpty => !string.IsNullOrEmpty(SubTitleText);

        private MagnifierShape MagnifierShape
        {
            get => _optionsService.MagnifierShape;
            set
            {
                if (_optionsService.MagnifierShape != value)
                {
                    _optionsService.MagnifierShape = value;

                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(IsMagnifierFrameSquare));
                }
            }
        }

        private void HandleRenderingMethodChangedEvent(object sender, System.EventArgs e)
        {
            RaisePropertyChanged(nameof(EngineIsFfmpeg));
            RaisePropertyChanged(nameof(EngineIsMediaFoundation));
        }

        private void InitCommands()
        {
            ToggleMagnifier = new RelayCommand(DoToggleMagnifier);
            ToggleMagnifierFrame = new RelayCommand(DoToggleMagnifierFrame);
            MagnifierSmaller = new RelayCommand(DoMagnifierSmaller);
            MagnifierLarger = new RelayCommand(DoMagnifierLarger);
        }

        private void DoMagnifierLarger()
        {
            switch (_optionsService.MagnifierSize)
            {
                case MagnifierSize.XXSmall:
                    _optionsService.MagnifierSize = MagnifierSize.XSmall;
                    break;

                case MagnifierSize.XSmall:
                    _optionsService.MagnifierSize = MagnifierSize.Small;
                    break;

                case MagnifierSize.Small:
                    _optionsService.MagnifierSize = MagnifierSize.Medium;
                    break;

                case MagnifierSize.Medium:
                    _optionsService.MagnifierSize = MagnifierSize.Large;
                    break;

                case MagnifierSize.Large:
                    _optionsService.MagnifierSize = MagnifierSize.XLarge;
                    break;

                case MagnifierSize.XLarge:
                    _optionsService.MagnifierSize = MagnifierSize.XXLarge;
                    break;
            }
        }

        private void DoMagnifierSmaller()
        {
            switch (_optionsService.MagnifierSize)
            {
                case MagnifierSize.XXLarge:
                    _optionsService.MagnifierSize = MagnifierSize.XLarge;
                    break;

                case MagnifierSize.XLarge:
                    _optionsService.MagnifierSize = MagnifierSize.Large;
                    break;

                case MagnifierSize.Large:
                    _optionsService.MagnifierSize = MagnifierSize.Medium;
                    break;

                case MagnifierSize.Medium:
                    _optionsService.MagnifierSize = MagnifierSize.Small;
                    break;

                case MagnifierSize.Small:
                    _optionsService.MagnifierSize = MagnifierSize.XSmall;
                    break;

                case MagnifierSize.XSmall:
                    _optionsService.MagnifierSize = MagnifierSize.XXSmall;
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

        private void DoToggleMagnifier()
        {
            IsMagnifierVisible = !IsMagnifierVisible;
        }

        private double CalculateMagnifierRadius()
        {
            const int minDelta = 10;
            var delta = Math.Max(WindowSize.Height / 40, minDelta);

            switch (_optionsService.MagnifierSize)
            {
                case MagnifierSize.XXSmall:
                    return delta;

                case MagnifierSize.XSmall:
                    return delta * 2;

                case MagnifierSize.Small:
                    return delta * 4;

                default:
                case MagnifierSize.Medium:
                    return delta * 6;

                case MagnifierSize.Large:
                    return delta * 8;

                case MagnifierSize.XLarge:
                    return delta * 12;

                case MagnifierSize.XXLarge:
                    return delta * 18;
            }
        }

        private void HandleMagnifierChangedEvent(object sender, System.EventArgs e)
        {
            RaisePropertyChanged(nameof(IsMagnifierVisible));
            RaisePropertyChanged(nameof(IsMagnifierFrameSquare));
            RaisePropertyChanged(nameof(MagnifierFrameType));
            RaisePropertyChanged(nameof(MagnifierZoomLevel));
            RaisePropertyChanged(nameof(MagnifierRadius));
            RaisePropertyChanged(nameof(MagnifierFrameThickness));
        }

        private void HandleBrowserChangedEvent(object sender, EventArgs e)
        {
            RaisePropertyChanged(nameof(BrowserZoomLevelIncrement));
        }
    }
}
