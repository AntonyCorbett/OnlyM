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

        public MediaViewModel(IOptionsService optionsService)
        {
            _optionsService = optionsService;
            _optionsService.RenderingMethodChangedEvent += HandleRenderingMethodChangedEvent;
            _optionsService.MagnifierChangedEvent += HandleMagnifierChangedEvent;

            InitCommands();
        }

        public bool EngineIsMediaFoundation => _optionsService.RenderingMethod == RenderingMethod.MediaFoundation;

        public bool EngineIsFfmpeg => _optionsService.RenderingMethod == RenderingMethod.Ffmpeg;

        public RelayCommand ToggleMagnifier { get; set; }

        public RelayCommand ToggleMagnifierFrame { get; set; }

        public IWpfWebBrowser WebBrowser
        {
            get => _webBrowser;
            set => Set(ref _webBrowser, value);
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

        public bool IsMagnifierVisible
        {
            get => _isMagnifierVisible && _optionsService.WebAllowMagnifier;
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

        public int DisplayWidth { get; set; }

        public int DisplayHeight { get; set; }

        public double MagnifierRadius => CalculateMagnifierRadius();

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
            var delta = DisplayHeight / 40;

            switch (_optionsService.MagnifierSize)
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
            RaisePropertyChanged(nameof(IsMagnifierVisible));
            RaisePropertyChanged(nameof(MagnifierFrameType));
            RaisePropertyChanged(nameof(MagnifierZoomLevel));
        }
    }
}
