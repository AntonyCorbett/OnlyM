namespace OnlyM.Services
{
    using System;
    using System.Threading.Tasks;
    using System.Windows.Controls;
    using MediaElementAdaption;
    using Models;
    using OnlyM.Core.Models;
    using OnlyM.Core.Subtitles;
    using Serilog;
    using Serilog.Events;

    internal sealed class VideoDisplayManager
    {
        private const int FreezeMillisecsFromEnd = 250;

        private readonly IMediaElement _mediaElement;
        private readonly TextBlock _subtitleBlock;

        private Guid _mediaItemId;
        private MediaClassification _mediaClassification;
        private TimeSpan _startPosition;
        private TimeSpan _lastPosition = TimeSpan.Zero;
        private bool _manuallySettingPlaybackPosition;
        private bool _firedNearEndEvent;
        private SubtitleProvider _subTitleProvider;
        private string _mediaItemFilePath;
        
        public VideoDisplayManager(IMediaElement mediaElement, TextBlock subtitleBlock)
        {
            _mediaElement = mediaElement;
            _subtitleBlock = subtitleBlock;

            _mediaElement.MediaOpened += HandleMediaOpened;
            _mediaElement.MediaClosed += HandleMediaClosed;
            _mediaElement.MediaEnded += async (s, e) => await HandleMediaEnded(s, e);
            _mediaElement.MediaFailed += HandleMediaFailed;
            _mediaElement.RenderingSubtitles += HandleRenderingSubtitles;
            _mediaElement.PositionChanged += HandlePositionChanged;
            _mediaElement.MessageLogged += HandleMediaElementMessageLogged;
        }

        public event EventHandler<MediaEventArgs> MediaChangeEvent;

        public event EventHandler<PositionChangedEventArgs> MediaPositionChangedEvent;

        public event EventHandler<MediaNearEndEventArgs> MediaNearEndEvent;

        public event EventHandler<SubtitleEventArgs> SubtitleEvent;

        public bool ShowSubtitles { get; set; }

        public bool IsPaused => _mediaElement.IsPaused;

        public async Task ShowVideoOrPlayAudio(
            string mediaItemFilePath,
            ScreenPosition screenPosition,
            Guid mediaItemId,
            MediaClassification mediaClassification,
            TimeSpan startOffset,
            bool startFromPaused)
        {
            _mediaItemId = mediaItemId;
            _mediaItemFilePath = mediaItemFilePath;

            Log.Debug($"ShowVideoOrPlayAudio - Media Id = {_mediaItemId}");
            
            _mediaClassification = mediaClassification;
            _startPosition = startOffset;
            _lastPosition = TimeSpan.Zero;

            ScreenPositionHelper.SetScreenPosition(_mediaElement.FrameworkElement, screenPosition);
            ScreenPositionHelper.SetSubtitleBlockScreenPosition(_subtitleBlock, screenPosition);
            
            _mediaElement.MediaItemId = mediaItemId;
            
            if (startFromPaused)
            {
                _mediaElement.Position = _startPosition;
                await _mediaElement.Play(new Uri(mediaItemFilePath), _mediaClassification);
                OnMediaChangeEvent(CreateMediaEventArgs(_mediaItemId, MediaChange.Started));

                CreateSubtitleProvider(mediaItemFilePath, startOffset);
            }
            else
            {
                Log.Debug($"Firing Started - Media Id = {_mediaItemId}");
                await _mediaElement.Play(new Uri(mediaItemFilePath), _mediaClassification).ConfigureAwait(true);
                OnMediaChangeEvent(CreateMediaEventArgs(_mediaItemId, MediaChange.Starting));
            }
        }

        public void SetPlaybackPosition(TimeSpan position)
        {
            _manuallySettingPlaybackPosition = true;

            _mediaElement.Position = position;
            _lastPosition = TimeSpan.Zero;

            _manuallySettingPlaybackPosition = false;
        }

        public TimeSpan GetPlaybackPosition()
        {
            return _mediaElement.Position;
        }

        public async Task PauseVideoOrAudioAsync(Guid mediaItemId)
        {
            if (_mediaItemId == mediaItemId)
            {
                await _mediaElement.Pause();
                OnMediaChangeEvent(CreateMediaEventArgs(_mediaItemId, MediaChange.Paused));

                _subTitleProvider?.Stop();
            }
        }

        public async Task HideVideoAsync(Guid mediaItemId)
        {
            if (_mediaItemId == mediaItemId)
            {
                OnMediaChangeEvent(CreateMediaEventArgs(_mediaItemId, MediaChange.Stopping));
                await _mediaElement.Close();

                _subTitleProvider?.Stop();
            }
        }

        private void OnMediaChangeEvent(MediaEventArgs e)
        {
            MediaChangeEvent?.Invoke(this, e);
        }

        private void HandleMediaOpened(object sender, System.Windows.RoutedEventArgs e)
        {
            Log.Logger.Information("Opened");

            _firedNearEndEvent = false;

            _mediaElement.Position = _startPosition;
            OnMediaChangeEvent(CreateMediaEventArgs(_mediaItemId, MediaChange.Started));

            CreateSubtitleProvider(_mediaItemFilePath, _startPosition);
        }

        private void HandleMediaClosed(object sender, System.Windows.RoutedEventArgs e)
        {
            Log.Logger.Debug("Media closed");

            _firedNearEndEvent = false;

            OnMediaChangeEvent(CreateMediaEventArgs(_mediaItemId, MediaChange.Stopped));
        }

        private async Task HandleMediaEnded(object sender, System.Windows.RoutedEventArgs e)
        {
            Log.Logger.Debug("Media ended");

            if (!_mediaElement.IsPaused)
            {
                OnMediaChangeEvent(CreateMediaEventArgs(_mediaItemId, MediaChange.Stopped));
                await _mediaElement.Close();
            }
        }

        private void HandleMediaFailed(object sender, System.Windows.ExceptionRoutedEventArgs e)
        {
            Log.Logger.Debug("Media failed");
            OnMediaChangeEvent(CreateMediaEventArgs(_mediaItemId, MediaChange.Stopped));
        }

        private void HandlePositionChanged(object sender, PositionChangedEventArgs e)
        {
            if (!_manuallySettingPlaybackPosition)
            {
                // only fire every 60ms
                if ((e.Position - _lastPosition).TotalMilliseconds > 60)
                {
                    _lastPosition = e.Position;
                    MediaPositionChangedEvent?.Invoke(this, e);

                    if (!_firedNearEndEvent &&
                        _mediaElement.NaturalDuration.HasTimeSpan &&
                        (_mediaElement.NaturalDuration.TimeSpan - e.Position).TotalMilliseconds < FreezeMillisecsFromEnd)
                    {
                        _firedNearEndEvent = true;
                        MediaNearEndEvent?.Invoke(this, new MediaNearEndEventArgs { MediaItemId = _mediaItemId });
                    }
                }
            }
        }

        private void HandleRenderingSubtitles(object sender, RenderSubtitlesEventArgs e)
        {
            e.Cancel = !ShowSubtitles;
        }

        private void HandleMediaElementMessageLogged(object sender, LogMessageEventArgs e)
        {
            switch (e.Level)
            {
                case LogEventLevel.Debug:
                    Log.Logger.Debug(e.Message);
                    break;

                case LogEventLevel.Error:
                    Log.Logger.Error(e.Message);
                    break;

                case LogEventLevel.Information:
                    Log.Logger.Information(e.Message);
                    break;

                case LogEventLevel.Verbose:
                    Log.Logger.Verbose(e.Message);
                    break;

                case LogEventLevel.Warning:
                    Log.Logger.Warning(e.Message);
                    break;
            }
        }

        private MediaEventArgs CreateMediaEventArgs(Guid id, MediaChange change)
        {
            return new MediaEventArgs
            {
                MediaItemId = id,
                Classification = _mediaClassification,
                Change = change
            };
        }

        private void HandleSubtitleEvent(object sender, SubtitleEventArgs e)
        {
            // only used in MediaFoundation as the other engines have their own
            // internal subtitle processing...
            if (e.Status == SubtitleStatus.NotShowing || ShowSubtitles)
            {
                SubtitleEvent?.Invoke(sender, e);
            }
        }

        private void CreateSubtitleProvider(string mediaItemFilePath, TimeSpan videoHeadPosition)
        {
            if (_subTitleProvider != null)
            {
                _subTitleProvider.SubtitleEvent -= HandleSubtitleEvent;
                _subTitleProvider = null;
            }

            if (_mediaClassification == MediaClassification.Video &&
                _mediaElement is MediaElementMediaFoundation)
            {
                var srtFile = SubtitleFileGenerator.Generate(mediaItemFilePath);
                _subTitleProvider = new SubtitleProvider(srtFile, videoHeadPosition);
                _subTitleProvider.SubtitleEvent += HandleSubtitleEvent;
            }
        }
    }
}
