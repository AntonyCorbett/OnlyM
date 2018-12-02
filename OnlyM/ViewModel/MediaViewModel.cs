namespace OnlyM.ViewModel
{
    using CefSharp.Wpf;
    using Core.Models;
    using Core.Services.Options;
    using GalaSoft.MvvmLight;
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
            
            InitCommands();
        }

        public bool EngineIsMediaFoundation => _optionsService.Options.RenderingMethod == RenderingMethod.MediaFoundation;

        public bool EngineIsFfmpeg => _optionsService.Options.RenderingMethod == RenderingMethod.Ffmpeg;

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

        public bool IsMagnifierFrameSquare
        {
            get => MagnifierFrameType == FrameType.Rectangle;
            set
            {
                if (MagnifierFrameType == FrameType.Rectangle != value)
                {
                    MagnifierFrameType = value ? FrameType.Rectangle : FrameType.Circle;
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

        public int MagnifierWidth
        {
            get => _optionsService.Options.MagnifierWidth;
            set
            {
                if (_optionsService.Options.MagnifierWidth != value)
                {
                    _optionsService.Options.MagnifierWidth = value;
                    RaisePropertyChanged();
                }
            }
        }

        public int MagnifierHeight
        {
            get => _optionsService.Options.MagnifierHeight;
            set
            {
                if (_optionsService.Options.MagnifierHeight != value)
                {
                    _optionsService.Options.MagnifierHeight = value;
                    RaisePropertyChanged();
                }
            }
        }

        public FrameType MagnifierFrameType
        {
            get => _optionsService.Options.MagnifierFrameType;
            set
            {
                if (_optionsService.Options.MagnifierFrameType != value)
                {
                    _optionsService.Options.MagnifierFrameType = value;
                    RaisePropertyChanged();
                }
            }
        }

        public int MagnifierRadius
        {
            get => _optionsService.Options.MagnifierRadius;
            set
            {
                if (_optionsService.Options.MagnifierRadius != value)
                {
                    _optionsService.Options.MagnifierRadius = value;
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

        public bool SubTitleTextIsNotEmpty => !string.IsNullOrEmpty(SubTitleText);
    
        private void HandleRenderingMethodChangedEvent(object sender, System.EventArgs e)
        {
            RaisePropertyChanged(nameof(EngineIsFfmpeg));
            RaisePropertyChanged(nameof(EngineIsMediaFoundation));
        }

        private void InitCommands()
        {
        }
    }
}
