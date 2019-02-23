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
    using OnlyM.Core.Services.Database;
    using OnlyM.Core.Services.Monitors;
    using OnlyM.CoreSys.Services.Snackbar;
    using OnlyM.Services.WebBrowser;
    using OnlyM.Services.WebNavHeaderManager;
    using OnlyM.ViewModel;
    using Serilog;
    using Services;
    using Services.Pages;
    
    /// <summary>
    /// Interaction logic for MediaWindow.xaml
    /// </summary>
    public sealed partial class MediaWindow : Window, IDisposable
    {
        private const int MediaConfirmStopWindowSeconds = 3;

        private readonly ImageDisplayManager _imageDisplayManager;
        private readonly WebDisplayManager _webDisplayManager;
        private readonly AudioManager _audioManager;

        private readonly WebNavHeaderAdmin _webNavHeaderAdmin;

        private readonly IOptionsService _optionsService;
        private readonly ISnackbarService _snackbarService;
        private VideoDisplayManager _videoDisplayManager;

        private IMediaElement _videoElement;
        private RenderingMethod _currentRenderingMethod;
        
        public MediaWindow(
            IOptionsService optionsService, 
            ISnackbarService snackbarService, 
            IDatabaseService databaseService,
            IMonitorsService monitorsService)
        {
            InitializeComponent();

            _webNavHeaderAdmin = new WebNavHeaderAdmin(WebNavHeader);

            _optionsService = optionsService;
            _snackbarService = snackbarService;

            _imageDisplayManager = new ImageDisplayManager(
                Image1Element, Image2Element, _optionsService);

            _webDisplayManager = new WebDisplayManager(
                Browser, BrowserGrid, databaseService, _optionsService, monitorsService, _snackbarService);

            _audioManager = new AudioManager();
            
            InitVideoRenderingMethod();

            SubscribeOptionsEvents();
            SubscribeImageEvents();
            SubscribeWebEvents();
            SubscribeAudioEvents();
        }

        public event EventHandler<MediaEventArgs> MediaChangeEvent;

        public event EventHandler<SlideTransitionEventArgs> SlideTransitionEvent;

        public event EventHandler<PositionChangedEventArgs> MediaPositionChangedEvent;

        public event EventHandler<MediaNearEndEventArgs> MediaNearEndEvent;

        public event EventHandler<WebBrowserProgressEventArgs> WebStatusEvent;

        public void Dispose()
        {
            _videoDisplayManager?.Dispose();
            _audioManager?.Dispose();
            VideoElementFfmpeg?.Dispose();
            Browser?.Dispose();
        }

        public void ShowMirror(bool show)
        {
            if (show)
            {
                _webDisplayManager.ShowMirror();
            }
            else
            {
                _webDisplayManager.CloseMirror();
            }
        }

        public void UpdateRenderingMethod()
        {
            if (_optionsService.RenderingMethod != _currentRenderingMethod)
            {
                InitVideoRenderingMethod();
            }
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
                    mediaItemToStart.PlaybackPositionChangedEvent -= HandleVideoPlaybackPositionChangedEvent;
                    await ShowVideoAsync(mediaItemToStart, currentMediaItems, startFromPaused);
                    break;

                case MediaClassification.Audio:
                    mediaItemToStart.PlaybackPositionChangedEvent -= HandleAudioPlaybackPositionChangedEvent;
                    PlayAudio(mediaItemToStart, startFromPaused);
                    break;

                case MediaClassification.Slideshow:
                    StartSlideshow(mediaItemToStart);
                    break;

                case MediaClassification.Web:
                    ShowWebPage(mediaItemToStart, currentMediaItems);
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
                    mediaItem.PlaybackPositionChangedEvent -= HandleAudioPlaybackPositionChangedEvent;
                    StopAudio(mediaItem);
                    break;

                case MediaClassification.Video:
                    mediaItem.PlaybackPositionChangedEvent -= HandleVideoPlaybackPositionChangedEvent;
                    await HideVideoAsync(mediaItem);
                    break;

                case MediaClassification.Slideshow:
                    StopSlideshow(mediaItem);
                    break;

                case MediaClassification.Web:
                    StopWeb(mediaItem);
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

            await PauseVideoOrAudioAsync(mediaItem);
        }

        public int GotoPreviousSlide()
        {
            return _imageDisplayManager.GotoPreviousSlide();
        }

        public int GotoNextSlide()
        {
            return _imageDisplayManager.GotoNextSlide();
        }

        private async Task PauseVideoOrAudioAsync(MediaItem mediaItem)
        {
            switch (mediaItem.MediaType.Classification)
            {
                case MediaClassification.Video:
                    await _videoDisplayManager.PauseVideoAsync(mediaItem.Id);
                    mediaItem.PlaybackPositionChangedEvent += HandleVideoPlaybackPositionChangedEvent;
                    break;

                case MediaClassification.Audio:
                    _audioManager.PauseAudio(mediaItem.Id);
                    mediaItem.PlaybackPositionChangedEvent += HandleAudioPlaybackPositionChangedEvent;
                    break;
            }
        }

        private async void HandleVideoPlaybackPositionChangedEvent(object sender, EventArgs e)
        {
            if (_optionsService.AllowVideoScrubbing)
            {
                var item = (MediaItem)sender;
                await _videoDisplayManager.SetPlaybackPosition(
                    TimeSpan.FromMilliseconds(item.PlaybackPositionDeciseconds * 100));
            }
        }

        private void HandleAudioPlaybackPositionChangedEvent(object sender, EventArgs e)
        {
            if (_optionsService.AllowVideoScrubbing)
            {
                var item = (MediaItem)sender;
                _audioManager.SetPlaybackPosition(
                    TimeSpan.FromMilliseconds(item.PlaybackPositionDeciseconds * 100));
            }
        }

        private async Task HideVideoAsync(MediaItem mediaItem)
        {
            await _videoDisplayManager.HideVideoAsync(mediaItem.Id);
        }

        private void StopAudio(MediaItem mediaItem)
        {
            _audioManager.StopAudio(mediaItem.Id);
        }

        private void HideImageOrSlideshow(IReadOnlyCollection<MediaItem> mediaItems)
        {
            var imageItem = mediaItems?.SingleOrDefault(
                x => x.MediaType.Classification == MediaClassification.Image ||
                     x.MediaType.Classification == MediaClassification.Slideshow);

            if (imageItem != null)
            {
                switch (imageItem.MediaType.Classification)
                {
                    case MediaClassification.Image:
                        _imageDisplayManager.HideSingleImage(imageItem.Id);
                        break;

                    case MediaClassification.Slideshow:
                        _imageDisplayManager.StopSlideshow(imageItem.Id);
                        break;
                }
            }
        }

        private void HideImage(MediaItem mediaItem)
        {
            if (mediaItem != null)
            {
                _imageDisplayManager.HideSingleImage(mediaItem.Id);
            }
        }
        
        private void ShowImage(MediaItem mediaItem)
        {
            _imageDisplayManager.ShowSingleImage(mediaItem.FilePath, mediaItem.Id, mediaItem.IsBlankScreen);
        }

        private void ShowWebPage(MediaItem mediaItem, IReadOnlyCollection<MediaItem> currentMediaItems)
        {
            var vm = (MediaViewModel)DataContext;
            vm.IsWebPage = !mediaItem.IsPdf;

            var showMirrorWindow = mediaItem.UseMirror && mediaItem.AllowUseMirror;

            _webDisplayManager.ShowWeb(
                mediaItem.FilePath, 
                mediaItem.Id,
                showMirrorWindow,
                _optionsService.WebScreenPosition);

            // show the header for a few seconds
            _webNavHeaderAdmin.PreviewWebNavHeader();

            HideImageOrSlideshow(currentMediaItems);
        }

        private void StopWeb(MediaItem mediaItem)
        {
            _webDisplayManager.HideWeb(mediaItem.FilePath);
        }

        private void StartSlideshow(MediaItem mediaItem)
        {
            mediaItem.CurrentSlideshowIndex = 0;
            _imageDisplayManager.StartSlideshow(mediaItem.FilePath, mediaItem.Id);
        }

        private void StopSlideshow(MediaItem mediaItem)
        {
            _imageDisplayManager.StopSlideshow(mediaItem.Id);
        }

        private async Task ShowVideoAsync(
            MediaItem mediaItemToStart,
            IReadOnlyCollection<MediaItem> currentMediaItems,
            bool startFromPaused)
        {
            var startPosition = TimeSpan.FromMilliseconds(mediaItemToStart.PlaybackPositionDeciseconds * 100);

            _videoDisplayManager.ShowSubtitles = _optionsService.ShowVideoSubtitles;

            await _videoDisplayManager.ShowVideoAsync(
                mediaItemToStart.FilePath,
                _optionsService.VideoScreenPosition,
                mediaItemToStart.Id,
                startPosition,
                startFromPaused);

            HideImageOrSlideshow(currentMediaItems);
        }

        private void PlayAudio(MediaItem mediaItemToStart, bool startFromPaused)
        {
            var startPosition = TimeSpan.FromMilliseconds(mediaItemToStart.PlaybackPositionDeciseconds * 100);

            _audioManager.PlayAudio(
                mediaItemToStart.FilePath,
                mediaItemToStart.Id,
                startPosition,
                startFromPaused);
        }

        private void WindowClosing(object sender, CancelEventArgs e)
        {
            // prevent window from being closed independently of application.
            var pageService = ServiceLocator.Current.GetInstance<IPageService>();
            e.Cancel = !pageService.ApplicationIsClosing && !pageService.AllowMediaWindowToClose;

            if (!e.Cancel)
            {
                UnsubscribeOptionsEvents();
                UnsubscribeImageEvents();
                UnsubscribeVideoEvents();
                UnsubscribeWebEvents();
                UnsubscribeAudioEvents();
            }
        }

        private void SubscribeOptionsEvents()
        {
            _optionsService.ShowSubtitlesChangedEvent += HandleShowSubtitlesChangedEvent;
            _optionsService.ImageScreenPositionChangedEvent += HandleImageScreenPositionChangedEvent;
            _optionsService.VideoScreenPositionChangedEvent += HandleVideoScreenPositionChangedEvent;
            _optionsService.WebScreenPositionChangedEvent += HandleWebScreenPositionChangedEvent;
        }

        private void SubscribeImageEvents()
        { 
            _imageDisplayManager.MediaChangeEvent += HandleMediaChangeEvent;
            _imageDisplayManager.SlideTransitionEvent += HandleSlideTransitionEvent;
        }

        private void SubscribeVideoEvents()
        {
            _videoDisplayManager.MediaChangeEvent += HandleMediaChangeEvent;
            _videoDisplayManager.MediaPositionChangedEvent += HandleMediaPositionChangedEvent;
            _videoDisplayManager.MediaNearEndEvent += HandleMediaNearEndEvent;
            _videoDisplayManager.SubtitleEvent += HandleMediaFoundationSubtitleEvent;
        }

        private void SubscribeWebEvents()
        {
            _webDisplayManager.MediaChangeEvent += HandleMediaChangeEvent;
            _webDisplayManager.StatusEvent += HandleWebDisplayManagerStatusEvent;
        }

        private void SubscribeAudioEvents()
        {
            _audioManager.MediaChangeEvent += HandleMediaChangeEvent;
            _audioManager.MediaPositionChangedEvent += HandleMediaPositionChangedEvent;
        }

        private void UnsubscribeAudioEvents()
        {
            _audioManager.MediaChangeEvent -= HandleMediaChangeEvent;
            _audioManager.MediaPositionChangedEvent -= HandleMediaPositionChangedEvent;
        }

        private void HandleWebDisplayManagerStatusEvent(object sender, WebBrowserProgressEventArgs e)
        {
            WebStatusEvent?.Invoke(this, e);
        }

        private void HandleMediaFoundationSubtitleEvent(object sender, Core.Subtitles.SubtitleEventArgs e)
        {
            var vm = (MediaViewModel)DataContext;

            vm.SubTitleText = e.Text == null 
                ? null 
                : string.Join(Environment.NewLine, e.Text);
        }

        private void UnsubscribeOptionsEvents()
        {
            _optionsService.ShowSubtitlesChangedEvent -= HandleShowSubtitlesChangedEvent;
            _optionsService.ImageScreenPositionChangedEvent -= HandleImageScreenPositionChangedEvent;
            _optionsService.VideoScreenPositionChangedEvent -= HandleVideoScreenPositionChangedEvent;
            _optionsService.WebScreenPositionChangedEvent -= HandleWebScreenPositionChangedEvent;
        }

        private void UnsubscribeImageEvents()
        {
            _imageDisplayManager.MediaChangeEvent -= HandleMediaChangeEvent;
            _imageDisplayManager.SlideTransitionEvent -= HandleSlideTransitionEvent;
        }

        private void UnsubscribeVideoEvents()
        {
            _videoDisplayManager.MediaChangeEvent -= HandleMediaChangeEvent;
            _videoDisplayManager.MediaPositionChangedEvent -= HandleMediaPositionChangedEvent;
            _videoDisplayManager.MediaNearEndEvent -= HandleMediaNearEndEvent;
            _videoDisplayManager.SubtitleEvent -= HandleMediaFoundationSubtitleEvent;
        }

        private void UnsubscribeWebEvents()
        {
            _webDisplayManager.MediaChangeEvent -= HandleMediaChangeEvent;
            _webDisplayManager.StatusEvent -= HandleWebDisplayManagerStatusEvent;
        }

        private void HandleMediaChangeEvent(object sender, MediaEventArgs e)
        {
            MediaChangeEvent?.Invoke(this, e);
        }

        private void HandleSlideTransitionEvent(object sender, SlideTransitionEventArgs e)
        {
            SlideTransitionEvent?.Invoke(this, e);
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
            _videoDisplayManager.ShowSubtitles = _optionsService.ShowVideoSubtitles;
        }

        private bool IsVideoOrAudio(MediaItem mediaItem)
        {
            return
                mediaItem.MediaType.Classification == MediaClassification.Audio ||
                mediaItem.MediaType.Classification == MediaClassification.Video;
        }

        private bool ShouldConfirmMediaStop(MediaItem mediaItem)
        {
            switch (mediaItem.MediaType.Classification)
            {
                case MediaClassification.Video:
                    return
                        _optionsService.ConfirmVideoStop &&
                        !_videoDisplayManager.IsPaused &&
                        _videoDisplayManager.GetPlaybackPosition().TotalSeconds > MediaConfirmStopWindowSeconds;

                case MediaClassification.Audio:
                    return
                        _optionsService.ConfirmVideoStop &&
                        !_audioManager.IsPaused &&
                        _audioManager.GetPlaybackPosition().TotalSeconds > MediaConfirmStopWindowSeconds;

                default:
                    return false;
            }
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
            ScreenPositionHelper.SetScreenPosition(_videoElement.FrameworkElement, _optionsService.VideoScreenPosition);
            ScreenPositionHelper.SetSubtitleBlockScreenPosition(SubtitleBlock, _optionsService.VideoScreenPosition);
        }

        private void HandleWebScreenPositionChangedEvent(object sender, EventArgs e)
        {
            ScreenPositionHelper.SetScreenPosition(BrowserGrid, _optionsService.WebScreenPosition);
        }

        private void HandleImageScreenPositionChangedEvent(object sender, EventArgs e)
        {
            ScreenPositionHelper.SetScreenPosition(Image1Element, _optionsService.ImageScreenPosition);
            ScreenPositionHelper.SetScreenPosition(Image2Element, _optionsService.ImageScreenPosition);
        }

        private void InitVideoRenderingMethod()
        {
            _videoElement?.UnsubscribeEvents();
            
            switch (_optionsService.RenderingMethod)
            {
                case RenderingMethod.Ffmpeg:
                    _videoElement = new MediaElementUnoSquare(VideoElementFfmpeg);
                    break;

                default:
                case RenderingMethod.MediaFoundation:
                    _videoElement = new MediaElementMediaFoundation(VideoElementMediaFoundation, _optionsService);
                    break;
            }

            _currentRenderingMethod = _optionsService.RenderingMethod;

            if (_videoDisplayManager != null)
            {
                UnsubscribeVideoEvents();
                _videoDisplayManager.Dispose();
            }

            _videoDisplayManager = new VideoDisplayManager(_videoElement, SubtitleBlock, _optionsService);
            
            SubscribeVideoEvents();
        }

        private void WindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            var vm = (MediaViewModel)DataContext;
            vm.WindowSize = new Size(ActualWidth, ActualHeight);
        }

        private void BrowserGrid_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var pos = e.GetPosition(this);
            _webNavHeaderAdmin.MouseMove(pos);
        }
    }
}
