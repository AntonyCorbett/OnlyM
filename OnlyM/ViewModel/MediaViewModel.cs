using System.Diagnostics;

namespace OnlyM.ViewModel
{
    using CefSharp.Wpf;
    using Core.Models;
    using Core.Services.Options;
    using GalaSoft.MvvmLight;
    using GalaSoft.MvvmLight.CommandWpf;
    using Xceed.Wpf.Toolkit;

    // ReSharper disable once ClassNeverInstantiated.Global
    internal class MediaViewModel : ViewModelBase
    {
        private readonly IOptionsService _optionsService;
        private string _subtitleText;
        private IWpfWebBrowser _webBrowser;
        private bool _isMagnifierVisible;
        private int _displayWidth;
        private int _displayHeight;

        public MediaViewModel(IOptionsService optionsService)
        {
            _optionsService = optionsService;
            _optionsService.RenderingMethodChangedEvent += HandleRenderingMethodChangedEvent;
            _optionsService.MagnifierChangedEvent += HandleMagnifierChangedEvent;
            
            InitCommands();
        }

        public bool EngineIsMediaFoundation => _optionsService.Options.RenderingMethod == RenderingMethod.MediaFoundation;

        public bool EngineIsFfmpeg => _optionsService.Options.RenderingMethod == RenderingMethod.Ffmpeg;

        public RelayCommand ToggleMagnifier { get; set; }

        public RelayCommand ToggleMagnifierFrame { get; set; }

        public IWpfWebBrowser WebBrowser
        {
            get => _webBrowser;
            set => Set(ref _webBrowser, value);
        }

        public double BrowserZoomLevelIncrement
        {
            get => _optionsService.Options.BrowserZoomLevelIncrement;
            set
            {
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (_optionsService.Options.BrowserZoomLevelIncrement != value)
                {
                    _optionsService.Options.BrowserZoomLevelIncrement = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool IsMagnifierVisible
        {
            get => _isMagnifierVisible && _optionsService.Options.WebAllowMagnifier;
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
            get => _optionsService.Options.MagnifierZoomLevel;
            set
            {
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (_optionsService.Options.MagnifierZoomLevel != value)
                {
                    _optionsService.Options.MagnifierZoomLevel = value;
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

        public int DisplayWidth
        {
            get => _displayWidth;
            set
            {
                if (_displayWidth != value)
                {
                    _displayWidth = value;
                    RaisePropertyChanged(nameof(MagnifierHeight));
                    RaisePropertyChanged(nameof(MagnifierWidth));
                }
            }
        }

        public int DisplayHeight
        {
            get => _displayHeight;
            set
            {
                if (_displayHeight != value)
                {
                    _displayHeight = value;
                    RaisePropertyChanged(nameof(MagnifierHeight));
                    RaisePropertyChanged(nameof(MagnifierWidth));
                }
            }
        }

        public MagnifierShape MagnifierShape
        {
            get => _optionsService.Options.MagnifierShape;
            set
            {
                if (_optionsService.Options.MagnifierShape != value)
                {
                    Debug.WriteLine($"CHANGING SHAPE ({_optionsService.Options.MagnifierShape}-{value})");
                    _optionsService.Options.MagnifierShape = value;
                    RaisePropertyChanged();
                }
            }
        }

        public int MagnifierWidth
        {
            get
            {
                var height = CalculateMagnifierHeight();
                var aspectRatioEllipse = 2.0;
                var aspectRatioRectangle = 2.0;

                switch (_optionsService.Options.MagnifierShape)
                {
                    case MagnifierShape.Ellipse:
                        Debug.WriteLine($"WIDTH={(int)(height * aspectRatioEllipse)}");
                        return (int)(height * aspectRatioEllipse);

                    case MagnifierShape.Rectangle:
                        Debug.WriteLine($"WIDTH={(int)(height * aspectRatioRectangle)}");
                        return (int)(height * aspectRatioRectangle);

                    default:
                    case MagnifierShape.Circle:
                    case MagnifierShape.Square:
                        Debug.WriteLine($"WIDTH={height}");
                        return height;
                }
            }
        }

        public int MagnifierHeight => CalculateMagnifierHeight();

        public FrameType MagnifierFrameType
        {
            get
            {
                switch (_optionsService.Options.MagnifierShape)
                {
                    case MagnifierShape.Rectangle:
                    case MagnifierShape.Square:
                        Debug.WriteLine("FRAME=RECT");
                        return FrameType.Rectangle;

                    default:
                    case MagnifierShape.Circle:
                    case MagnifierShape.Ellipse:
                        Debug.WriteLine("FRAME=CIRCLE");
                        return FrameType.Circle;
                }
            } 
        }

        public bool SubTitleTextIsNotEmpty => !string.IsNullOrEmpty(SubTitleText);
    
        private void HandleRenderingMethodChangedEvent(object sender, System.EventArgs e)
        {
            RaisePropertyChanged(nameof(EngineIsFfmpeg));
            RaisePropertyChanged(nameof(EngineIsMediaFoundation));
        }

        private void InitCommands()
        {
            ToggleMagnifier = new RelayCommand(DoToggleMagnifier);
            ToggleMagnifierFrame = new RelayCommand(DoToggleMagnifierFrame);
        }

        private void DoToggleMagnifierFrame()
        {
            switch (MagnifierShape)
            {
                case MagnifierShape.Circle:
                    Debug.WriteLine("TO ELLIPSE");
                    MagnifierShape = MagnifierShape.Ellipse;
                    break;

                case MagnifierShape.Ellipse:
                    Debug.WriteLine("TO SQUARE");
                    MagnifierShape = MagnifierShape.Square;
                    break;

                case MagnifierShape.Square:
                    Debug.WriteLine("TO RECT");
                    MagnifierShape = MagnifierShape.Rectangle;
                    break;

                case MagnifierShape.Rectangle:
                    Debug.WriteLine("TO CIRCLE");
                    MagnifierShape = MagnifierShape.Circle;
                    break;
            }
        }

        private void DoToggleMagnifier()
        {
            IsMagnifierVisible = !IsMagnifierVisible;
        }

        private int CalculateMagnifierHeight()
        {
            var delta = _displayHeight / 40;

            switch (_optionsService.Options.MagnifierSize)
            {
                case MagnifierSize.XSmall:
                    return delta;

                case MagnifierSize.Small:
                    return delta * 2;

                case MagnifierSize.Large:
                    return delta * 8;

                case MagnifierSize.XLarge:
                    return delta * 16;

                default:
                case MagnifierSize.Normal:
                    return delta * 4;
            }
        }

        private void HandleMagnifierChangedEvent(object sender, System.EventArgs e)
        {
            Debug.WriteLine("Handling Magnifier Changed Event");
            RaisePropertyChanged(nameof(IsMagnifierVisible));
            RaisePropertyChanged(nameof(MagnifierFrameType));
            RaisePropertyChanged(nameof(MagnifierWidth));
            RaisePropertyChanged(nameof(MagnifierHeight));
            RaisePropertyChanged(nameof(MagnifierZoomLevel));
        }
    }
}
