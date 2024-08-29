using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using OnlyM.Core.Models;
using OnlyM.Core.Services.Options;

namespace OnlyM.MediaElementAdaption
{
    internal sealed class MediaElementMediaFoundation : IMediaElement
    {
        // note that _audioPlayer is used for audio-only playback (e.g. MP3 files).
        // Videos are rendered using _mediaElement. It should be possible to use MediaElement
        // exclusively, but it must be part of the visual tree in order to work correctly
        // and we want to be able to play audio without the need to create the MediaWindow.
        private readonly Lazy<MediaPlayer> _audioPlayer;
        private readonly MediaElement _mediaElement;
        private readonly DispatcherTimer _timer;
        private readonly IOptionsService _optionsService;
        private MediaClassification _currentMediaClassification;

        public MediaElementMediaFoundation(
            MediaElement mediaElement,
            IOptionsService optionsService)
        {
            _currentMediaClassification = MediaClassification.Unknown;

            _optionsService = optionsService;

            _audioPlayer = new Lazy<MediaPlayer>(MediaPlayerFactory);

            _mediaElement = mediaElement;
            _mediaElement.Volume = 1.0; // max = 1.0

            _mediaElement.ScrubbingEnabled = optionsService.AllowVideoScrubbing;

            _mediaElement.MediaOpened += HandleMediaOpened;
            _mediaElement.MediaEnded += HandleMediaEnded;
            _mediaElement.MediaFailed += HandleMediaFailed;
            
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
            _timer.Tick += TimerFire;
        }

        public event EventHandler<OnlyMMediaOpenedEventArgs>? MediaOpened;

        public event EventHandler<OnlyMMediaClosedEventArgs>? MediaClosed;

        public event EventHandler<OnlyMMediaEndedEventArgs>? MediaEnded;

        public event EventHandler<OnlyMMediaFailedEventArgs>? MediaFailed;

        // not supported in MediaFoundation
        public event EventHandler<OnlyMRenderSubtitlesEventArgs>? RenderingSubtitles
        {
            add { }
            remove { }
        }

        public event EventHandler<OnlyMPositionChangedEventArgs>? PositionChanged;

        // not supported in MediaFoundation
        public event EventHandler<OnlyMLogMessageEventArgs>? MessageLogged
        {
            add { }
            remove { }
        }

        public bool IsPaused { get; private set; }

        public FrameworkElement FrameworkElement => _mediaElement;

        public Guid MediaItemId { get; set; }

        public TimeSpan Position
        {
            get
            {
                if (_currentMediaClassification == MediaClassification.Audio)
                {
                    return _audioPlayer.Value.Position;
                }

                return _mediaElement.Position;
            }

            set
            {
                if (_currentMediaClassification == MediaClassification.Audio)
                {
                    _audioPlayer.Value.Position = value;
                }
                else
                {
                    _mediaElement.Position = value;
                }
            } 
        }

        public Duration NaturalDuration
        {
            get
            {
                if (_currentMediaClassification == MediaClassification.Audio)
                {
                    return _audioPlayer.Value.NaturalDuration;
                }

                return _mediaElement.NaturalDuration;
            }
        } 

        public Task Play(Uri mediaPath, MediaClassification mediaClassification)
        {
            _currentMediaClassification = mediaClassification;

            if (_currentMediaClassification == MediaClassification.Audio)
            {
                if (!IsPaused)
                {
                    _audioPlayer.Value.Open(mediaPath);
                }

                IsPaused = false;
                _audioPlayer.Value.Play();
            }
            else
            {
                IsPaused = false;

                if (_mediaElement.Source != mediaPath)
                {
                    _mediaElement.Source = mediaPath;
                }

                _mediaElement.Play();
            }

            _timer.Start();

            return Task.CompletedTask;
        }

        public Task Pause()
        {
            IsPaused = true;

            if (_currentMediaClassification == MediaClassification.Audio)
            {
                _audioPlayer.Value.Pause();
            }
            else
            {
                _mediaElement.Pause();
            }
                
            return Task.CompletedTask;
        }

        public Task Close()
        {
            _timer.Stop();

            if (_currentMediaClassification == MediaClassification.Audio)
            {
                _audioPlayer.Value.Close();
            }
            else
            {
                _mediaElement.Close();
            }

            IsPaused = false;

            MediaClosed?.Invoke(this, new OnlyMMediaClosedEventArgs());

            return Task.CompletedTask;
        }

        public void UnsubscribeEvents()
        {
            _mediaElement.MediaOpened -= HandleMediaOpened;
            _mediaElement.MediaEnded -= HandleMediaEnded;
            _mediaElement.MediaFailed -= HandleMediaFailed;

            if (_audioPlayer.IsValueCreated)
            {
                _audioPlayer.Value.MediaOpened -= HandleMediaOpened2;
                _audioPlayer.Value.MediaEnded -= HandleMediaEnded2;
                _audioPlayer.Value.MediaFailed -= HandleMediaFailed2;
            }

            _timer.Tick -= TimerFire;
        }

        private void HandleMediaOpened(object? sender, RoutedEventArgs? e)
        {
            MediaOpened?.Invoke(sender, new OnlyMMediaOpenedEventArgs());
        }

        private void HandleMediaEnded(object? sender, RoutedEventArgs? e)
        {
            MediaEnded?.Invoke(sender, new OnlyMMediaEndedEventArgs());
        }

        private void HandleMediaFailed(object? sender, ExceptionRoutedEventArgs? e)
        {
            MediaFailed?.Invoke(sender, new OnlyMMediaFailedEventArgs { Error = e?.ErrorException });
        }

        private void TimerFire(object? sender, EventArgs e)
        {
            PositionChanged?.Invoke(this, new OnlyMPositionChangedEventArgs(MediaItemId, Position));
        }

        private MediaPlayer MediaPlayerFactory()
        {
            var result = new MediaPlayer
            {
                Volume = 1.0,
                ScrubbingEnabled = _optionsService.AllowVideoScrubbing,
            };

            result.MediaOpened += HandleMediaOpened2;
            result.MediaEnded += HandleMediaEnded2;
            result.MediaFailed += HandleMediaFailed2;
            
            return result;
        }

        private void HandleMediaFailed2(object? sender, ExceptionEventArgs e)
        {
            HandleMediaFailed(sender, null);
        }

        private void HandleMediaEnded2(object? sender, EventArgs e)
        {
            HandleMediaEnded(sender, null);
        }

        private void HandleMediaOpened2(object? sender, EventArgs e)
        {
            HandleMediaOpened(sender, null);
        }
    }
}
