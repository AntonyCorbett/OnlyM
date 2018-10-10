namespace OnlyM.ViewModel
{
    using Core.Models;
    using Core.Services.Options;
    using GalaSoft.MvvmLight;

    // ReSharper disable once ClassNeverInstantiated.Global
    internal class MediaViewModel : ViewModelBase
    {
        private readonly IOptionsService _optionsService;

        public MediaViewModel(IOptionsService optionsService)
        {
            _optionsService = optionsService;
            _optionsService.RenderingMethodChangedEvent += HandleRenderingMethodChangedEvent;
        }

        public bool EngineIsMediaFoundation => _optionsService.Options.RenderingMethod == RenderingMethod.MediaFoundation;

        public bool EngineIsFfmpeg => _optionsService.Options.RenderingMethod == RenderingMethod.Ffmpeg;

        private void HandleRenderingMethodChangedEvent(object sender, System.EventArgs e)
        {
            RaisePropertyChanged(nameof(EngineIsFfmpeg));
            RaisePropertyChanged(nameof(EngineIsMediaFoundation));
        }
    }
}
