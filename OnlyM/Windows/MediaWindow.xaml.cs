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
    using Models;
    using Services;
    using Services.Pages;
    using Unosquare.FFME.Events;

    /// <summary>
    /// Interaction logic for MediaWindow.xaml
    /// </summary>
    public partial class MediaWindow : Window
    {
        private readonly ImageDisplayManager _imageDisplayManager;
        private readonly VideoDisplayManager _videoDisplayManager;

        public event EventHandler<MediaEventArgs> MediaChangeEvent;

        public event EventHandler<PositionChangedRoutedEventArgs> MediaPositionChangedEvent;

        public MediaWindow(IOptionsService optionsService)
        {
            InitializeComponent();

            _imageDisplayManager = new ImageDisplayManager(Image1Element, Image2Element, optionsService);
            _imageDisplayManager.MediaChangeEvent += HandleMediaChangeEvent;

            _videoDisplayManager = new VideoDisplayManager(VideoElement);
            _videoDisplayManager.MediaChangeEvent += HandleMediaChangeEvent;
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
            bool showSubtitles, 
            bool startFromPaused)
        {
            switch (mediaItemToStart.MediaType.Classification)
            {
                case MediaClassification.Image:
                    ShowImage(mediaItemToStart);
                    break;

                case MediaClassification.Video:
                case MediaClassification.Audio:
                    await ShowVideo(mediaItemToStart, currentMediaItem, showSubtitles, startFromPaused);
                    break;
            }
        }

        public void CacheImageItem(MediaItem mediaItem)
        {
            _imageDisplayManager.CacheImageItem(mediaItem.FilePath);
        }

        public async Task StopMediaAsync(MediaItem mediaItem)
        {
            switch (mediaItem.MediaType.Classification)
            {
                case MediaClassification.Image:
                    HideImage(mediaItem);
                    break;

                case MediaClassification.Video:
                case MediaClassification.Audio:
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
                         
            await PauseVideoAsync(mediaItem);
        }

        private async Task PauseVideoAsync(MediaItem mediaItem)
        {
            await _videoDisplayManager.PauseVideoAsync(mediaItem.Id);
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
            _imageDisplayManager.ShowImage(mediaItem.FilePath, mediaItem.Id);
        }

        private async Task ShowVideo(
            MediaItem mediaItemToStart, 
            MediaItem currentMediaItem, 
            bool showSubtitles, 
            bool startFromPaused)
        {
            HideImage(currentMediaItem);

            var startPosition = TimeSpan.FromMilliseconds(mediaItemToStart.PlaybackPositionDeciseconds * 10);

            await _videoDisplayManager.ShowVideo(
                mediaItemToStart.FilePath, 
                mediaItemToStart.Id, 
                startPosition, 
                showSubtitles, 
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
            e.Cancel = !pageService.ApplicationIsClosing;
        }

        private void HandleMediaChangeEvent(object sender, MediaEventArgs e)
        {
            MediaChangeEvent?.Invoke(this, e);
        }

        private void HandleMediaPositionChangedEvent(object sender, PositionChangedRoutedEventArgs e)
        {
            MediaPositionChangedEvent?.Invoke(this, e);
        }
    }
}
