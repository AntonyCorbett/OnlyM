using Unosquare.FFME.Common;

namespace OnlyM.MediaElementAdaption
{
    using System;
    using System.Threading.Tasks;
    using System.Windows;
    using OnlyM.Core.Models;
    using OnlyM.Core.Utils;
    using Serilog.Events;
    
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

        public event EventHandler<MediaOpenedEventArgs> MediaOpened;

        public event EventHandler<EventArgs> MediaClosed;

        public event EventHandler<EventArgs> MediaEnded;

        public event EventHandler<MediaFailedEventArgs> MediaFailed;

        public event EventHandler<RenderSubtitlesEventArgs> RenderingSubtitles;

        public event EventHandler<PositionChangedEventArgs> PositionChanged;

        public event EventHandler<LogMessageEventArgs> MessageLogged;

        public TimeSpan Position
        {
            get => _mediaElement.Position;
            set => _mediaElement.Position = value;
        }

        public Duration NaturalDuration => new(_mediaElement.NaturalDuration ?? default);

        public FrameworkElement FrameworkElement => _mediaElement;

        public Guid MediaItemId { get; set; }

        public bool IsPaused { get; private set; }

        public async Task Play(Uri mediaPath, MediaClassification mediaClassification)
        {
            IsPaused = false;

            mediaPath = FFmpegUtils.FixUnicodeUri(mediaPath);

            if (_mediaElement.Source != mediaPath)
            {
                await _mediaElement.Open(mediaPath);
            }
            else
            {
                await _mediaElement.Play();
            }
        }

        public async Task Pause()
        {
            IsPaused = true;
            await _mediaElement.Pause();
        }

        public async Task Close()
        {
            IsPaused = false;
            await _mediaElement.Close();
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

        private async void HandleMediaOpened(object sender, MediaOpenedEventArgs e)
        {
            await _mediaElement.Play();
            MediaOpened?.Invoke(sender, e);
        }

        private void HandleMediaClosed(object sender, EventArgs e)
        {
            MediaClosed?.Invoke(sender, e);
        }

        private void HandleMediaEnded(object sender, EventArgs e)
        {
            MediaEnded?.Invoke(sender, e);
        }

        private void HandleMediaFailed(object sender, MediaFailedEventArgs e)
        {
            MediaFailed?.Invoke(sender, e);
        }

        private void HandleRenderingSubtitles(object sender, RenderingSubtitlesEventArgs e)
        {
            var args = new RenderSubtitlesEventArgs { Cancel = e.Cancel };
            RenderingSubtitles?.Invoke(sender, args);
            e.Cancel = args.Cancel;
        }

        private void HandlePositionChanged(object sender, Unosquare.FFME.Common.PositionChangedEventArgs e)
        {
            PositionChanged?.Invoke(sender, new PositionChangedEventArgs(MediaItemId, e.Position));
        }

        private void HandleMessageLogged(object sender, MediaLogMessageEventArgs e)
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
