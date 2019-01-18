namespace OnlyM.Services
{
    using System;
    using System.Timers;
    using NAudio.Wave;
    using OnlyM.Core.Models;
    using OnlyM.MediaElementAdaption;
    using OnlyM.Models;
    using Serilog;

    internal sealed class AudioManager : IDisposable
    {
        private readonly Timer _timer = new Timer(200);

        private Guid _mediaItemId;
        private WaveOutEvent _outputDevice;
        private AudioFileReader _audioFileReader;
        private bool _manuallySettingPlaybackPosition;
        private string _mediaItemFilePath;
        
        public AudioManager()
        {
            _timer.Elapsed += HandleTimerFire;    
        }

        public event EventHandler<MediaEventArgs> MediaChangeEvent;

        public event EventHandler<PositionChangedEventArgs> MediaPositionChangedEvent;

        public bool IsPaused => _outputDevice != null && _outputDevice.PlaybackState == PlaybackState.Paused;

        public void PlayAudio(
            string mediaItemFilePath,
            Guid mediaItemId,
            TimeSpan startPosition,
            bool startFromPaused)
        {
            _mediaItemId = mediaItemId;
            _mediaItemFilePath = mediaItemFilePath;

            if (!startFromPaused)
            {
                OnMediaChangeEvent(CreateMediaEventArgs(_mediaItemId, MediaChange.Starting));
            }

            Log.Debug($"PlayAudio - Media Id = {_mediaItemId}");

            if (_outputDevice == null)
            {
                _outputDevice = new WaveOutEvent();
                _outputDevice.PlaybackStopped += OnPlaybackStopped;
            }

            if (_audioFileReader == null)
            {
                _audioFileReader = new AudioFileReader(_mediaItemFilePath);
                _outputDevice.Init(_audioFileReader);
            }

            if (!startFromPaused && startPosition != TimeSpan.Zero)
            {
                _audioFileReader.SetPosition(startPosition);
            }

            _outputDevice.Play();
            _timer.Start();
            
            OnMediaChangeEvent(CreateMediaEventArgs(_mediaItemId, MediaChange.Started));
        }

        public void Dispose()
        {
            _timer?.Dispose();
            _outputDevice?.Dispose();
            _audioFileReader?.Dispose();
        }

        public TimeSpan GetPlaybackPosition()
        {
            return _audioFileReader.GetPosition();
        }

        public void PauseAudio(Guid mediaItemId)
        {
            if (_mediaItemId == mediaItemId && _outputDevice != null)
            {
                _outputDevice.Pause();
                _timer.Stop();
                OnMediaChangeEvent(CreateMediaEventArgs(_mediaItemId, MediaChange.Paused));
            }
        }
        
        public void StopAudio(Guid mediaItemId)
        {
            if (_mediaItemId == mediaItemId)
            {
                OnMediaChangeEvent(CreateMediaEventArgs(_mediaItemId, MediaChange.Stopping));
                _outputDevice?.Stop();
            }
        }
        
        public void SetPlaybackPosition(TimeSpan position)
        {
            _manuallySettingPlaybackPosition = true;

            if (position == TimeSpan.Zero)
            {
                StopAudio(_mediaItemId);
            }
            else
            {
                _audioFileReader?.SetPosition(position);
            }
            
            _manuallySettingPlaybackPosition = false;
        }

        private void OnPlaybackStopped(object sender, StoppedEventArgs e)
        {
            _timer.Stop();

            _outputDevice.Dispose();
            _outputDevice = null;

            _audioFileReader.Dispose();
            _audioFileReader = null;

            OnMediaChangeEvent(CreateMediaEventArgs(_mediaItemId, MediaChange.Stopped));
        }

        private void OnMediaChangeEvent(MediaEventArgs e)
        {
            MediaChangeEvent?.Invoke(this, e);
        }

        private MediaEventArgs CreateMediaEventArgs(Guid id, MediaChange change)
        {
            return new MediaEventArgs
            {
                MediaItemId = id,
                Classification = MediaClassification.Audio,
                Change = change
            };
        }

        private void HandleTimerFire(object sender, ElapsedEventArgs e)
        {
            if (!_manuallySettingPlaybackPosition)
            {
                MediaPositionChangedEvent?.Invoke(this, new PositionChangedEventArgs(_mediaItemId, GetPlaybackPosition()));
            }
        }
    }
}
