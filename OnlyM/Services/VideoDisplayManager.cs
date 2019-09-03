namespace OnlyM.Services
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using System.Windows.Controls;
    using GalaSoft.MvvmLight.Messaging;
    using OnlyM.Core.Models;
    using OnlyM.Core.Services.Options;
    using OnlyM.Core.Subtitles;
    using OnlyM.MediaElementAdaption;
    using OnlyM.Models;
    using OnlyM.PubSubMessages;
    using Serilog;
    using Serilog.Events;

    internal sealed class VideoDisplayManager : IDisposable
    {
        private const int FreezeMillisecsFromEnd = 250;

        private readonly IMediaElement _mediaElement;
        private readonly TextBlock _subtitleBlock;

        private readonly IOptionsService _optionsService;

        private Guid _mediaItemId;
        private TimeSpan _startPosition;
        private TimeSpan _lastPosition = TimeSpan.Zero;
        private bool _manuallySettingPlaybackPosition;
        private bool _firedNearEndEvent;
        private SubtitleProvider _subTitleProvider;
        private string _mediaItemFilePath;

        public VideoDisplayManager(IMediaElement mediaElement, TextBlock subtitleBlock, IOptionsService optionsService)
        {
            _mediaElement = mediaElement;
            _subtitleBlock = subtitleBlock;

            _optionsService = optionsService;

            SubscribeEvents();
        }

        public event EventHandler<MediaEventArgs> MediaChangeEvent;

        public event EventHandler<PositionChangedEventArgs> MediaPositionChangedEvent;

        public event EventHandler<MediaNearEndEventArgs> MediaNearEndEvent;

        public event EventHandler<SubtitleEventArgs> SubtitleEvent;
        
        public bool ShowSubtitles { get; set; }

        public bool IsPaused => _mediaElement.IsPaused;

        public void Dispose()
        {
            UnsubscribeEvents();
        }

        public async Task ShowVideoAsync(
            string mediaItemFilePath,
            ScreenPosition screenPosition,
            Guid mediaItemId,
            TimeSpan startOffset,
            bool startFromPaused)
        {
            _mediaItemId = mediaItemId;
            _mediaItemFilePath = mediaItemFilePath;

            Log.Debug($"ShowVideo - Media Id = {_mediaItemId}");

            _startPosition = startOffset;
            _lastPosition = TimeSpan.Zero;

            ScreenPositionHelper.SetScreenPosition(_mediaElement.FrameworkElement, screenPosition);
            ScreenPositionHelper.SetSubtitleBlockScreenPosition(_subtitleBlock, screenPosition);
            
            _mediaElement.MediaItemId = mediaItemId;
            
            if (startFromPaused)
            {
                _mediaElement.Position = _startPosition;
                await _mediaElement.Play(new Uri(mediaItemFilePath), MediaClassification.Video);
                OnMediaChangeEvent(CreateMediaEventArgs(_mediaItemId, MediaChange.Started));

                await CreateSubtitleProvider(mediaItemFilePath, startOffset);
            }
            else
            {
                Log.Debug($"Firing Started - Media Id = {_mediaItemId}");

                await CreateSubtitleFile(mediaItemFilePath);

                await _mediaElement.Play(new Uri(mediaItemFilePath), MediaClassification.Video).ConfigureAwait(true);
                OnMediaChangeEvent(CreateMediaEventArgs(_mediaItemId, MediaChange.Starting));
            }
        }

        public async Task SetPlaybackPosition(TimeSpan position)
        {
            _manuallySettingPlaybackPosition = true;

            if (position == TimeSpan.Zero)
            {
                await HideVideoAsync(_mediaItemId);
            }
            else
            {
                _mediaElement.Position = position;
            }

            _lastPosition = TimeSpan.Zero;
            _manuallySettingPlaybackPosition = false;
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

        private async void HandleMediaOpened(object sender, System.Windows.RoutedEventArgs e)
        {
            Log.Logger.Information("Opened");

            _firedNearEndEvent = false;

            _mediaElement.Position = _startPosition;
            OnMediaChangeEvent(CreateMediaEventArgs(_mediaItemId, MediaChange.Started));

            await CreateSubtitleProvider(_mediaItemFilePath, _startPosition);
        }

        private void HandleMediaClosed(object sender, System.Windows.RoutedEventArgs e)
        {
            Log.Logger.Debug("Media closed");

            _firedNearEndEvent = false;

            OnMediaChangeEvent(CreateMediaEventArgs(_mediaItemId, MediaChange.Stopped));
        }

        private async void HandleMediaEnded(object sender, System.Windows.RoutedEventArgs e)
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
                Classification = MediaClassification.Video,
                Change = change,
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

        private Task<string> CreateSubtitleFile(string mediaItemFilePath)
        {
            return Task.Run(() =>
            {
                if (_mediaElement is MediaElementMediaFoundation &&
                    _optionsService.ShowVideoSubtitles)
                {
                    return SubtitleFileGenerator.Generate(mediaItemFilePath, _mediaItemId);
                }

                return null;
            });
        }

        private async Task CreateSubtitleProvider(string mediaItemFilePath, TimeSpan videoHeadPosition)
        {
            if (_subTitleProvider != null)
            {
                _subTitleProvider.SubtitleEvent -= HandleSubtitleEvent;
                _subTitleProvider = null;
            }

            if (_mediaElement is MediaElementMediaFoundation &&
                _optionsService.ShowVideoSubtitles)
            {
                // accommodate any latency introduced by creation of srt file
                var sw = Stopwatch.StartNew();
                var srtFile = await CreateSubtitleFile(mediaItemFilePath);
                
                videoHeadPosition += sw.Elapsed;

                _subTitleProvider = new SubtitleProvider(srtFile, videoHeadPosition);
                _subTitleProvider.SubtitleEvent += HandleSubtitleEvent;
            }
        }

        private void HandleSubtitleFileEvent(object sender, SubtitleFileEventArgs e)
        {
            Messenger.Default.Send(new SubtitleFileMessage { MediaItemId = e.MediaItemId, Starting = e.Starting });
        }

        private void SubscribeEvents()
        {
            _mediaElement.MediaOpened += HandleMediaOpened;
            _mediaElement.MediaClosed += HandleMediaClosed;
            _mediaElement.MediaEnded += HandleMediaEnded;
            _mediaElement.MediaFailed += HandleMediaFailed;
            _mediaElement.RenderingSubtitles += HandleRenderingSubtitles;
            _mediaElement.PositionChanged += HandlePositionChanged;
            _mediaElement.MessageLogged += HandleMediaElementMessageLogged;

            SubtitleFileGenerator.SubtitleFileEvent += HandleSubtitleFileEvent;
        }

        private void UnsubscribeEvents()
        {
            _mediaElement.MediaOpened -= HandleMediaOpened;
            _mediaElement.MediaClosed -= HandleMediaClosed;
            _mediaElement.MediaEnded -= HandleMediaEnded;
            _mediaElement.MediaFailed -= HandleMediaFailed;
            _mediaElement.RenderingSubtitles -= HandleRenderingSubtitles;
            _mediaElement.PositionChanged -= HandlePositionChanged;
            _mediaElement.MessageLogged -= HandleMediaElementMessageLogged;

            SubtitleFileGenerator.SubtitleFileEvent -= HandleSubtitleFileEvent;
        }
    }
}
