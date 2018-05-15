namespace OnlyM.Services
{
    using System;
    using System.Threading.Tasks;
    using Models;
    using Unosquare.FFME;

    internal sealed class VideoDisplayManager
    {
        private readonly MediaElement _mediaElement;
        private Guid _mediaItemId;

        public event EventHandler<MediaEventArgs> MediaChangeEvent;

        public VideoDisplayManager(MediaElement mediaElement)
        {
            _mediaElement = mediaElement;
            _mediaElement.MediaOpened += HandleMediaOpened;
            _mediaElement.MediaClosed += HandleMediaClosed;
            _mediaElement.MediaEnded += HandleMediaEnded;
            _mediaElement.MediaFailed += HandleMediaFailed;
        }

        public void ShowVideo(string mediaItemFilePath, Guid mediaItemId)
        {
            _mediaItemId = mediaItemId;

            OnMediaChangeEvent(new MediaEventArgs { MediaItemId = _mediaItemId, Change = MediaChange.Starting });

            _mediaElement.Source = new Uri(mediaItemFilePath);
        }

        public async Task HideVideoAsync(Guid mediaItemId)
        {
            if (_mediaItemId == mediaItemId)
            {
                OnMediaChangeEvent(new MediaEventArgs { MediaItemId = _mediaItemId, Change = MediaChange.Stopping });

                await _mediaElement.Stop();
                await _mediaElement.Close();
            }
        }

        private void OnMediaChangeEvent(MediaEventArgs e)
        {
            MediaChangeEvent?.Invoke(this, e);
        }

        private void HandleMediaOpened(object sender, System.Windows.RoutedEventArgs e)
        {
            OnMediaChangeEvent(new MediaEventArgs { MediaItemId = _mediaItemId, Change = MediaChange.Started });
        }

        private void HandleMediaClosed(object sender, System.Windows.RoutedEventArgs e)
        {
            OnMediaChangeEvent(new MediaEventArgs { MediaItemId = _mediaItemId, Change = MediaChange.Stopped });
        }

        private void HandleMediaEnded(object sender, System.Windows.RoutedEventArgs e)
        {
            OnMediaChangeEvent(new MediaEventArgs { MediaItemId = _mediaItemId, Change = MediaChange.Stopped });
        }

        private void HandleMediaFailed(object sender, System.Windows.ExceptionRoutedEventArgs e)
        {
            OnMediaChangeEvent(new MediaEventArgs { MediaItemId = _mediaItemId, Change = MediaChange.Stopped });
        }
    }
}
