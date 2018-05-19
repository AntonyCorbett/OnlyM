namespace OnlyM.Services
{
    using System;
    using System.Threading.Tasks;
    using Models;
    using Serilog;
    using Unosquare.FFME;

    internal sealed class VideoDisplayManager
    {
        private readonly MediaElement _mediaElement;
        private Guid _mediaItemId;
        private TimeSpan _startPosition;
        
        public event EventHandler<MediaEventArgs> MediaChangeEvent;

        public event EventHandler<Unosquare.FFME.Events.PositionChangedRoutedEventArgs> MediaPositionChangedEvent;

        public VideoDisplayManager(MediaElement mediaElement)
        {
            _mediaElement = mediaElement;
            _mediaElement.MediaOpened += HandleMediaOpened;
            _mediaElement.MediaClosed += HandleMediaClosed;
            _mediaElement.MediaEnded += async (s, e) => await HandleMediaEnded(s, e);
            _mediaElement.MediaFailed += HandleMediaFailed;
            
            _mediaElement.RenderingSubtitles += HandleRenderingSubtitles;
            
            _mediaElement.PositionChanged += HandlePositionChanged;
        }

        public bool ShowSubtitles { get; set; }

        public async Task ShowVideo(
            string mediaItemFilePath, 
            Guid mediaItemId, 
            TimeSpan startOffset, 
            bool startFromPaused)
        {
            _mediaItemId = mediaItemId;
            _startPosition = startOffset;

            if (startFromPaused)
            {
                _mediaElement.Position = _startPosition;
                await _mediaElement.Play();
                OnMediaChangeEvent(new MediaEventArgs { MediaItemId = _mediaItemId, Change = MediaChange.Started });
            }
            else
            {
                OnMediaChangeEvent(new MediaEventArgs { MediaItemId = _mediaItemId, Change = MediaChange.Starting });
                _mediaElement.Source = new Uri(mediaItemFilePath);
            }
        }

        public void SetPlaybackPosition(TimeSpan position)
        {
            _mediaElement.PositionChanged -= HandlePositionChanged;
            _mediaElement.Position = position;
            _mediaElement.PositionChanged += HandlePositionChanged;
        }

        public TimeSpan GetPlaybackPosition()
        {
            return _mediaElement.Position;
        }

        public async Task PauseVideoAsync(Guid mediaItemId)
        {
            if (_mediaItemId == mediaItemId)
            {
                await _mediaElement.Pause();
                OnMediaChangeEvent(new MediaEventArgs { MediaItemId = _mediaItemId, Change = MediaChange.Paused });
            }
        }

        public async Task HideVideoAsync(Guid mediaItemId)
        {
            if (_mediaItemId == mediaItemId)
            {
                OnMediaChangeEvent(new MediaEventArgs { MediaItemId = _mediaItemId, Change = MediaChange.Stopping });
                await _mediaElement.Close();
            }
        }

        private void OnMediaChangeEvent(MediaEventArgs e)
        {
            MediaChangeEvent?.Invoke(this, e);
        }

        private void HandleMediaOpened(object sender, System.Windows.RoutedEventArgs e)
        {
            Log.Logger.Information("Opened");
            _mediaElement.Position = _startPosition;
            OnMediaChangeEvent(new MediaEventArgs
            {
                MediaItemId = _mediaItemId,
                Change = MediaChange.Started
            });
        }

        private void HandleMediaClosed(object sender, System.Windows.RoutedEventArgs e)
        {
            Log.Logger.Debug("Media closed");
            OnMediaChangeEvent(new MediaEventArgs { MediaItemId = _mediaItemId, Change = MediaChange.Stopped });
        }

        private async Task HandleMediaEnded(object sender, System.Windows.RoutedEventArgs e)
        {
            Log.Logger.Debug("Media ended");
            OnMediaChangeEvent(new MediaEventArgs { MediaItemId = _mediaItemId, Change = MediaChange.Stopped });
            await _mediaElement.Close();
        }

        private void HandleMediaFailed(object sender, System.Windows.ExceptionRoutedEventArgs e)
        {
            Log.Logger.Debug("Media failed");
            OnMediaChangeEvent(new MediaEventArgs { MediaItemId = _mediaItemId, Change = MediaChange.Stopped });
        }

        private void HandlePositionChanged(object sender, Unosquare.FFME.Events.PositionChangedRoutedEventArgs e)
        {
            MediaPositionChangedEvent?.Invoke(this, e);
        }

        private void HandleRenderingSubtitles(object sender, Unosquare.FFME.Events.RenderingSubtitlesEventArgs e)
        {
            e.Cancel = !ShowSubtitles;
        }
    }
}
