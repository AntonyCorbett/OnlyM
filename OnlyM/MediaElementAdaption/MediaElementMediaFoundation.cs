namespace OnlyM.MediaElementAdaption
{
    using System;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Threading;
    using Core.Services.Options;

    internal sealed class MediaElementMediaFoundation : IMediaElement
    {
        private readonly MediaElement _mediaElement;
        private readonly DispatcherTimer _timer;


        public MediaElementMediaFoundation(
            MediaElement mediaElement,
            IOptionsService optionsService)
        {
            _mediaElement = mediaElement;
            _mediaElement.Volume = 1.0;

            _mediaElement.ScrubbingEnabled = optionsService.Options.AllowVideoScrubbing;

            _mediaElement.MediaOpened += HandleMediaOpened;
            _mediaElement.MediaEnded += HandleMediaEnded;
            _mediaElement.MediaFailed += HandleMediaFailed;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
            _timer.Tick += TimerFire;
        }

        public event EventHandler<RoutedEventArgs> MediaOpened;

        public event EventHandler<RoutedEventArgs> MediaClosed;

        public event EventHandler<RoutedEventArgs> MediaEnded;

        public event EventHandler<ExceptionRoutedEventArgs> MediaFailed;

        // not supported in MediaFoundation
        public event EventHandler<RenderSubtitlesEventArgs> RenderingSubtitles
        {
            add { }
            remove { }
        }

        public event EventHandler<PositionChangedEventArgs> PositionChanged;

        // not supported in MediaFoundation
        public event EventHandler<LogMessageEventArgs> MessageLogged
        {
            add { }
            remove { }
        }

        public TimeSpan Position
        {
            get => _mediaElement.Position;
            set => _mediaElement.Position = value;
        }

        public Duration NaturalDuration => _mediaElement.NaturalDuration;

        public Task Play(Uri mediaPath)
        {
            IsPaused = false;

            if (_mediaElement.Source != mediaPath)
            {
                _mediaElement.Source = mediaPath;
            }

            _mediaElement.Play();
            
            _timer.Start();
            return Task.CompletedTask;
        }

        public Task Pause()
        {
            IsPaused = true;
            _mediaElement.Pause();
            return Task.CompletedTask;
        }

        public Task Close()
        {
            _timer.Stop();
            _mediaElement.Close();

            MediaClosed?.Invoke(this, null);
            return Task.CompletedTask;
        }

        public bool IsPaused { get; private set; }

        public FrameworkElement FrameworkElement => _mediaElement;

        public Guid MediaItemId { get; set; }

        private void HandleMediaOpened(object sender, RoutedEventArgs e)
        {
            MediaOpened?.Invoke(sender, e);
        }

        private void HandleMediaEnded(object sender, RoutedEventArgs e)
        {
            MediaEnded?.Invoke(sender, e);
        }

        private void HandleMediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            MediaFailed?.Invoke(sender, e);
        }

        private void TimerFire(object sender, EventArgs e)
        {
            PositionChanged?.Invoke(this, new PositionChangedEventArgs(MediaItemId, Position));
        }
    }
}
