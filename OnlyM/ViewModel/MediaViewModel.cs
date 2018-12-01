namespace OnlyM.ViewModel
{
    using CefSharp.Wpf;
    using Core.Models;
    using Core.Services.Options;
    using GalaSoft.MvvmLight;
    using GalaSoft.MvvmLight.CommandWpf;

    // ReSharper disable once ClassNeverInstantiated.Global
    internal class MediaViewModel : ViewModelBase
    {
        private readonly IOptionsService _optionsService;
        private string _subtitleText;
        private IWpfWebBrowser _webBrowser;

        public MediaViewModel(IOptionsService optionsService)
        {
            _optionsService = optionsService;
            _optionsService.RenderingMethodChangedEvent += HandleRenderingMethodChangedEvent;
        }

        public bool EngineIsMediaFoundation => _optionsService.Options.RenderingMethod == RenderingMethod.MediaFoundation;

        public bool EngineIsFfmpeg => _optionsService.Options.RenderingMethod == RenderingMethod.Ffmpeg;
        
        public IWpfWebBrowser WebBrowser
        {
            get => _webBrowser;
            set => Set(ref _webBrowser, value);
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
    }
}
