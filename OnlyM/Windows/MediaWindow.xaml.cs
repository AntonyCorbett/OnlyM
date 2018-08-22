namespace OnlyM.Windows
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Linq;
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
        private readonly IOptionsService _optionsService;
        private readonly ISnackbarService _snackbarService;

        private VideoDisplayManager _videoDisplayManager;
        private IMediaElement _videoElement;
        private RenderingMethod _currentRenderingMethod;

        public event EventHandler<MediaEventArgs> MediaChangeEvent;

        public event EventHandler<PositionChangedEventArgs> MediaPositionChangedEvent;

        public event EventHandler<MediaNearEndEventArgs> MediaNearEndEvent;

        public MediaWindow(IOptionsService optionsService, ISnackbarService snackbarService)
        {
            InitializeComponent();

            _optionsService = optionsService;

            _imageDisplayManager = new ImageDisplayManager(Image1Element, Image2Element, _optionsService);
            
            _snackbarService = snackbarService;

            InitRenderingMethod();

            SubscribeEvents();
        }

        public void UpdateRenderingMethod()
        {
            if (_optionsService.Options.RenderingMethod != _currentRenderingMethod)
            {
                InitRenderingMethod();
            }
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
            IReadOnlyCollection<MediaItem> currentMediaItems,
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
                    await ShowVideoOrPlayAudio(mediaItemToStart, currentMediaItems, startFromPaused);
                    break;
            }
        }

        public void CacheImageItem(MediaItem mediaItem)
        {
            _imageDisplayManager.CacheImageItem(mediaItem.FilePath);
        }

        public async Task StopMediaAsync(
            MediaItem mediaItem,
            bool ignoreConfirmation = false)
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
                    HideImage(mediaItem);
                    break;

                case MediaClassification.Audio:
                case MediaClassification.Video:
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
                    TimeSpan.FromMilliseconds(item.PlaybackPositionDeciseconds * 100));
            }
        }

        private async Task HideVideoAsync(MediaItem mediaItem)
        {
            await _videoDisplayManager.HideVideoAsync(mediaItem.Id);
        }

        private void HideImage(IReadOnlyCollection<MediaItem> mediaItems)
        {
            var imageItem = mediaItems?.SingleOrDefault(x => x.MediaType.Classification == MediaClassification.Image);
            if (imageItem != null)
            {
                _imageDisplayManager.HideImage(imageItem.Id);
            }
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
            _imageDisplayManager.ShowImage(
                mediaItem.FilePath, 
                _optionsService.Options.ImageScreenPosition,
                mediaItem.Id, 
                mediaItem.IsBlankScreen);
        }

        private async Task ShowVideoOrPlayAudio(
            MediaItem mediaItemToStart,
            IReadOnlyCollection<MediaItem> currentMediaItems,
            bool startFromPaused)
        {
            if (mediaItemToStart.MediaType.Classification != MediaClassification.Audio)
            {
                HideImage(currentMediaItems);
            }

            var startPosition = TimeSpan.FromMilliseconds(mediaItemToStart.PlaybackPositionDeciseconds * 100);

            _videoDisplayManager.ShowSubtitles = _optionsService.Options.ShowVideoSubtitles;

            await _videoDisplayManager.ShowVideoOrPlayAudio(
                mediaItemToStart.FilePath, 
                _optionsService.Options.VideoScreenPosition,
                mediaItemToStart.Id,
                mediaItemToStart.MediaType.Classification,
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

            if (!e.Cancel)
            {
                UnsubscribeEvents();
            }
        }

        private void SubscribeEvents()
        {
            _optionsService.ShowSubtitlesChangedEvent += HandleShowSubtitlesChangedEvent;
            _optionsService.ImageScreenPositionChangedEvent += HandleImageScreenPositionChangedEvent;
            _optionsService.VideoScreenPositionChangedEvent += HandleVideoScreenPositionChangedEvent;

            _imageDisplayManager.MediaChangeEvent += HandleMediaChangeEvent;

            SubscribeVideoDisplayManagerEvents();
        }

        private void SubscribeVideoDisplayManagerEvents()
        {
            _videoDisplayManager.MediaChangeEvent += HandleMediaChangeEvent;
            _videoDisplayManager.MediaPositionChangedEvent += HandleMediaPositionChangedEvent;
            _videoDisplayManager.MediaNearEndEvent += HandleMediaNearEndEvent;
        }

        private void UnsubscribeEvents()
        {
            _optionsService.ShowSubtitlesChangedEvent -= HandleShowSubtitlesChangedEvent;
            _optionsService.ImageScreenPositionChangedEvent -= HandleImageScreenPositionChangedEvent;
            _optionsService.VideoScreenPositionChangedEvent -= HandleVideoScreenPositionChangedEvent;

            _imageDisplayManager.MediaChangeEvent -= HandleMediaChangeEvent;

            UnsubscribeVideoDisplayManagerEvents();
        }

        private void UnsubscribeVideoDisplayManagerEvents()
        {
            _videoDisplayManager.MediaChangeEvent -= HandleMediaChangeEvent;
            _videoDisplayManager.MediaPositionChangedEvent -= HandleMediaPositionChangedEvent;
            _videoDisplayManager.MediaNearEndEvent -= HandleMediaNearEndEvent;
        }

        private void HandleMediaChangeEvent(object sender, MediaEventArgs e)
        {
            MediaChangeEvent?.Invoke(this, e);
        }

        private void HandleMediaPositionChangedEvent(object sender, PositionChangedEventArgs e)
        {
            MediaPositionChangedEvent?.Invoke(this, e);
        }

        private void HandleMediaNearEndEvent(object sender, MediaNearEndEventArgs e)
        {
            MediaNearEndEvent?.Invoke(this, e);
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

        private void HandleVideoScreenPositionChangedEvent(object sender, EventArgs e)
        {
            ScreenPositionHelper.SetScreenPosition(_videoElement.FrameworkElement, _optionsService.Options.VideoScreenPosition);
        }

        private void HandleImageScreenPositionChangedEvent(object sender, EventArgs e)
        {
            ScreenPositionHelper.SetScreenPosition(Image1Element, _optionsService.Options.ImageScreenPosition);
            ScreenPositionHelper.SetScreenPosition(Image2Element, _optionsService.Options.ImageScreenPosition);
        }

        private void InitRenderingMethod()
        {
            switch (_optionsService.Options.RenderingMethod)
            {
                case RenderingMethod.Ffmpeg:
                    _videoElement = new MediaElementUnoSquare(VideoElementFfmpeg);
                    break;

                default:
                case RenderingMethod.MediaFoundation:
                    _videoElement = new MediaElementMediaFoundation(VideoElementMediaFoundation, _optionsService);
                    break;
            }

            _currentRenderingMethod = _optionsService.Options.RenderingMethod;

            if (_videoDisplayManager != null)
            {
                UnsubscribeVideoDisplayManagerEvents();
            }

            _videoDisplayManager = new VideoDisplayManager(_videoElement);

            SubscribeVideoDisplayManagerEvents();
        }
    }
}
