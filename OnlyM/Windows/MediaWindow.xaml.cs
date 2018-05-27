namespace OnlyM.Windows
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using System.Windows;
    using CommonServiceLocator;
    using Core.Models;
    using Core.Services.Options;
    using MediaElementAdaption;
    using Models;
    using Serilog;
    using Services;
    using Services.Pages;
    using Services.Snackbar;

    /// <summary>
    /// Interaction logic for MediaWindow.xaml
    /// </summary>
    public partial class MediaWindow : Window
    {
        private const int MediaConfirmStopWindowSeconds = 3;

        private readonly ImageDisplayManager _imageDisplayManager;
        private readonly VideoDisplayManager _videoDisplayManager;
        private readonly IOptionsService _optionsService;
        private readonly ISnackbarService _snackbarService;

        public event EventHandler<MediaEventArgs> MediaChangeEvent;

        public event EventHandler<PositionChangedEventArgs> MediaPositionChangedEvent;

        public event EventHandler FinishedWithWindowEvent;

        private bool _finishingWithImageDisplay;

        public MediaWindow(IOptionsService optionsService, ISnackbarService snackbarService)
        {
            InitializeComponent();

            _optionsService = optionsService;
            _optionsService.ShowSubtitlesChangedEvent += HandleShowSubtitlesChangedEvent;

            _imageDisplayManager = new ImageDisplayManager(Image1Element, Image2Element, _optionsService);
            _imageDisplayManager.MediaChangeEvent += HandleMediaChangeEventForImages;

            _snackbarService = snackbarService;
            
            _videoDisplayManager = new VideoDisplayManager(new MediaElementUnoSquare(VideoElement));
            
            _videoDisplayManager.MediaChangeEvent += HandleMediaChangeEventForVideoAndAudio;
            _videoDisplayManager.MediaPositionChangedEvent += HandleMediaPositionChangedEvent;
        }

        public ImageFadeType ImageFadeType
        {
            set => _imageDisplayManager.ImageFadeType = value;
        }

        public FadeSpeed ImageFadeSpeed
        {
            set => _imageDisplayManager.ImageFadeSpeed = value;
        }

        public async Task StartMedia(
            MediaItem mediaItemToStart, 
            MediaItem currentMediaItem, 
            bool startFromPaused)
        {
            Log.Logger.Information($"Starting media {mediaItemToStart.FilePath}");

            switch (mediaItemToStart.MediaType.Classification)
            {
                case MediaClassification.Image:
                    ShowImage(mediaItemToStart);
                    break;

                case MediaClassification.Video:
                case MediaClassification.Audio:
                    mediaItemToStart.PlaybackPositionChangedEvent -= HandlePlaybackPositionChangedEvent;
                    await ShowVideo(mediaItemToStart, currentMediaItem, startFromPaused);
                    break;
            }
        }

        public void CacheImageItem(MediaItem mediaItem)
        {
            _imageDisplayManager.CacheImageItem(mediaItem.FilePath);
        }

        public async Task StopMediaAsync(MediaItem mediaItem, bool ignoreConfirmation = false)
        {
            if (!ignoreConfirmation && ShouldConfirmMediaStop(mediaItem))
            {
                ConfirmMediaStop(mediaItem);
                return;
            }

            Log.Logger.Information($"Stopping media {mediaItem.FilePath}");

            switch (mediaItem.MediaType.Classification)
            {
                case MediaClassification.Image:
                    _finishingWithImageDisplay = true;
                    HideImage(mediaItem);
                    break;

                case MediaClassification.Video:
                    mediaItem.PlaybackPositionChangedEvent -= HandlePlaybackPositionChangedEvent;
                    await HideVideoAsync(mediaItem);
                    break;

                case MediaClassification.Audio:
                    mediaItem.PlaybackPositionChangedEvent -= HandlePlaybackPositionChangedEvent;
                    await HideVideoAsync(mediaItem);
                    break;
            }
        }

        public async Task PauseMediaAsync(MediaItem mediaItem)
        {
            Debug.Assert(
                mediaItem.MediaType.Classification == MediaClassification.Audio ||
                mediaItem.MediaType.Classification == MediaClassification.Video,
                "Expecting audio or video media item");

            Log.Logger.Information($"Pausing media {mediaItem.FilePath}");

            await PauseVideoAsync(mediaItem);
        }

        private async Task PauseVideoAsync(MediaItem mediaItem)
        {
            await _videoDisplayManager.PauseVideoAsync(mediaItem.Id);
            mediaItem.PlaybackPositionChangedEvent += HandlePlaybackPositionChangedEvent;
        }

        private void HandlePlaybackPositionChangedEvent(object sender, EventArgs e)
        {
            if (_optionsService.Options.AllowVideoScrubbing)
            {
                var item = (MediaItem)sender;
                _videoDisplayManager.SetPlaybackPosition(
                    TimeSpan.FromMilliseconds(item.PlaybackPositionDeciseconds * 10));
            }
        }

        private async Task HideVideoAsync(MediaItem mediaItem)
        {
            await _videoDisplayManager.HideVideoAsync(mediaItem.Id);
        }

        private void HideImage(MediaItem mediaItem)
        {
            if (mediaItem != null)
            {
                _imageDisplayManager.HideImage(mediaItem.Id);
            }
        }

        private void ShowImage(MediaItem mediaItem)
        {
            _imageDisplayManager.ShowImage(mediaItem.FilePath, mediaItem.Id, mediaItem.IsBlankScreen);
        }

        private async Task ShowVideo(
            MediaItem mediaItemToStart, 
            MediaItem currentMediaItem, 
            bool startFromPaused)
        {
            HideImage(currentMediaItem);

            var startPosition = TimeSpan.FromMilliseconds(mediaItemToStart.PlaybackPositionDeciseconds * 10);

            _videoDisplayManager.ShowSubtitles = _optionsService.Options.ShowVideoSubtitles;

            await _videoDisplayManager.ShowVideo(
                mediaItemToStart.FilePath, 
                mediaItemToStart.Id, 
                startPosition, 
                startFromPaused);
        }

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Maximized;
        }

        private void WindowClosing(object sender, CancelEventArgs e)
        {
            // prevent window from being closed independently of application.
            var pageService = ServiceLocator.Current.GetInstance<IPageService>();
            e.Cancel = !pageService.ApplicationIsClosing && !pageService.AllowMediaWindowToClose;
        }

        private void HandleMediaChangeEventForImages(object sender, MediaEventArgs e)
        {
            MediaChangeEvent?.Invoke(this, e);

            if (e.Change == MediaChange.Stopped && _finishingWithImageDisplay)
            {
                _finishingWithImageDisplay = false;
                OnFinishedWithWindowEvent();
            }
        }

        private void HandleMediaChangeEventForVideoAndAudio(object sender, MediaEventArgs e)
        {
            MediaChangeEvent?.Invoke(this, e);

            if (e.Change == MediaChange.Stopped)
            {
                OnFinishedWithWindowEvent();
            }
        }

        private void HandleMediaPositionChangedEvent(object sender, PositionChangedEventArgs e)
        {
            MediaPositionChangedEvent?.Invoke(this, e);
        }

        private void OnFinishedWithWindowEvent()
        {
            FinishedWithWindowEvent?.Invoke(this, EventArgs.Empty);
        }

        private void HandleShowSubtitlesChangedEvent(object sender, EventArgs e)
        {
            _videoDisplayManager.ShowSubtitles = _optionsService.Options.ShowVideoSubtitles;
        }

        private bool IsVideoOrAudio(MediaItem mediaItem)
        {
            return
                mediaItem.MediaType.Classification == MediaClassification.Audio ||
                mediaItem.MediaType.Classification == MediaClassification.Video;
        }

        private bool ShouldConfirmMediaStop(MediaItem mediaItem)
        {
            return
                _optionsService.Options.ConfirmVideoStop &&
                IsVideoOrAudio(mediaItem) &&
                !_videoDisplayManager.IsPaused && 
                _videoDisplayManager.GetPlaybackPosition().TotalSeconds > MediaConfirmStopWindowSeconds;
        }

        private void ConfirmMediaStop(MediaItem mediaItem)
        {
            _snackbarService.Enqueue(
                Properties.Resources.CONFIRM_STOP_MEDIA,
                Properties.Resources.YES,
                async (obj) => { await StopMediaAsync(mediaItem, ignoreConfirmation: true); },
                null,
                promote: true,
                neverConsiderToBeDuplicate: true);
        }
    }
}
