namespace OnlyM.MediaElementAdaption
{
    using System;
    using System.Threading.Tasks;
    using System.Windows;
    using OnlyM.Core.Models;
    using Serilog.Events;
    using Unosquare.FFME.Shared;

    internal sealed class MediaElementUnoSquare : IMediaElement
    {
        private readonly Unosquare.FFME.MediaElement _mediaElement;
        
        public MediaElementUnoSquare(Unosquare.FFME.MediaElement mediaElement)
        {
            _mediaElement = mediaElement;
            _mediaElement.Volume = 1.0;
            
            _mediaElement.MediaOpened += HandleMediaOpened;
            _mediaElement.MediaClosed += HandleMediaClosed;
            _mediaElement.MediaEnded += HandleMediaEnded;
            _mediaElement.MediaFailed += HandleMediaFailed;
            _mediaElement.RenderingSubtitles += HandleRenderingSubtitles;
            _mediaElement.PositionChanged += HandlePositionChanged;
            _mediaElement.MessageLogged += HandleMessageLogged;
        }

        public event EventHandler<RoutedEventArgs> MediaOpened;

        public event EventHandler<RoutedEventArgs> MediaClosed;

        public event EventHandler<RoutedEventArgs> MediaEnded;

        public event EventHandler<ExceptionRoutedEventArgs> MediaFailed;

        public event EventHandler<RenderSubtitlesEventArgs> RenderingSubtitles;

        public event EventHandler<PositionChangedEventArgs> PositionChanged;

        public event EventHandler<LogMessageEventArgs> MessageLogged;

        public TimeSpan Position
        {
            get => _mediaElement.Position;
            set => _mediaElement.Position = value;
        }

        public Duration NaturalDuration => _mediaElement.NaturalDuration;

        public FrameworkElement FrameworkElement => _mediaElement;

        public Guid MediaItemId { get; set; }

        public bool IsPaused { get; private set; }

        public async Task Play(Uri mediaPath, MediaClassification mediaClassification)
        {
            IsPaused = false;

            if (_mediaElement.Source != mediaPath)
            {
                _mediaElement.Source = mediaPath;
            }
            else
            {
                await _mediaElement.Play();
            }
        }

        public Task Pause()
        {
            IsPaused = true;
            return _mediaElement.Pause();
        }

        public Task Close()
        {
            IsPaused = false;
            return _mediaElement.Close();
        }

        public void UnsubscribeEvents()
        {
            _mediaElement.MediaOpened -= HandleMediaOpened;
            _mediaElement.MediaClosed -= HandleMediaClosed;
            _mediaElement.MediaEnded -= HandleMediaEnded;
            _mediaElement.MediaFailed -= HandleMediaFailed;
            _mediaElement.RenderingSubtitles -= HandleRenderingSubtitles;
            _mediaElement.PositionChanged -= HandlePositionChanged;
            _mediaElement.MessageLogged -= HandleMessageLogged;
        }

        private async void HandleMediaOpened(object sender, RoutedEventArgs e)
        {
            await _mediaElement.Play();
            MediaOpened?.Invoke(sender, e);
        }

        private void HandleMediaClosed(object sender, RoutedEventArgs e)
        {
            MediaClosed?.Invoke(sender, e);
        }

        private void HandleMediaEnded(object sender, RoutedEventArgs e)
        {
            MediaEnded?.Invoke(sender, e);
        }

        private void HandleMediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            MediaFailed?.Invoke(sender, e);
        }

        private void HandleRenderingSubtitles(object sender, Unosquare.FFME.Events.RenderingSubtitlesEventArgs e)
        {
            var args = new RenderSubtitlesEventArgs { Cancel = e.Cancel };
            RenderingSubtitles?.Invoke(sender, args);
            e.Cancel = args.Cancel;
        }

        private void HandlePositionChanged(object sender, Unosquare.FFME.Events.PositionChangedRoutedEventArgs e)
        {
            PositionChanged?.Invoke(sender, new PositionChangedEventArgs(MediaItemId, e.Position));
        }

        private void HandleMessageLogged(object sender, Unosquare.FFME.Events.MediaLogMessageEventArgs e)
        {
            var level = LogEventLevel.Information;

            switch (e.MessageType)
            {
                case MediaLogMessageType.Debug:
                    level = LogEventLevel.Debug;
                    break;

                case MediaLogMessageType.Error:
                    level = LogEventLevel.Error;
                    break;

                case MediaLogMessageType.Info:
                    level = LogEventLevel.Information;
                    break;

                case MediaLogMessageType.Trace:
                    level = LogEventLevel.Verbose;
                    break;

                case MediaLogMessageType.Warning:
                    level = LogEventLevel.Warning;
                    break;
            }

            MessageLogged?.Invoke(sender, new LogMessageEventArgs(level, e.Message));
        }
    }
}
