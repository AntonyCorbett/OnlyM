namespace OnlyM.MediaElementAdaption
{
    using System;
    using System.Threading.Tasks;
    using System.Windows;
    using Serilog.Events;
    using Unosquare.FFME.Shared;

    internal class MediaElementUnoSquare : IMediaElement
    {
        private readonly Unosquare.FFME.MediaElement _mediaElement;

        public MediaElementUnoSquare(Unosquare.FFME.MediaElement mediaElement)
        {
            _mediaElement = mediaElement;

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

        public Task Play()
        {
            return _mediaElement.Play();
        }

        public Task Pause()
        {
            return _mediaElement.Pause();
        }

        public Task Close()
        {
            return _mediaElement.Close();
        }

        public Uri Source
        {
            get => _mediaElement.Source;
            set => _mediaElement.Source = value;
        }

        public bool IsPaused => _mediaElement.IsPaused;

        private void HandleMediaOpened(object sender, RoutedEventArgs e)
        {
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
            PositionChanged?.Invoke(sender, new PositionChangedEventArgs(e.OldPosition, e.Position));
        }

        private void HandleMessageLogged(object sender, Unosquare.FFME.Events.MediaLogMessageEventArgs e)
        {
            Serilog.Events.LogEventLevel level = LogEventLevel.Information;

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
