﻿using System;
using System.Timers;
using NAudio.Wave;
using OnlyM.Core.Models;
using OnlyM.MediaElementAdaption;
using OnlyM.Models;
using Serilog;

namespace OnlyM.Services;

internal sealed class AudioManager : IDisposable
{
    private readonly Timer _timer = new(200);

    private Guid _mediaItemId;
    private WaveOutEvent? _outputDevice;
    private AudioFileReader? _audioFileReader;
    private bool _manuallySettingPlaybackPosition;

    public AudioManager()
    {
        _timer.Elapsed += HandleTimerFire;
    }

    public event EventHandler<MediaEventArgs>? MediaChangeEvent;

    public event EventHandler<OnlyMPositionChangedEventArgs>? MediaPositionChangedEvent;

    public bool IsPaused => _outputDevice?.PlaybackState == PlaybackState.Paused;

    public void PlayAudio(
        string mediaItemFilePath,
        Guid mediaItemId,
        TimeSpan startPosition,
        bool startFromPaused)
    {
        _mediaItemId = mediaItemId;

        if (!startFromPaused)
        {
            OnMediaChangeEvent(CreateMediaEventArgs(_mediaItemId, MediaChange.Starting));
        }

        Log.Debug("PlayAudio - Media Id = {MediaItemId}", _mediaItemId);

        if (_outputDevice == null)
        {
            _outputDevice = new WaveOutEvent();
            _outputDevice.PlaybackStopped += OnPlaybackStopped;
        }

        if (_audioFileReader == null)
        {
            _audioFileReader = new AudioFileReader(mediaItemFilePath)
            {
                Volume = 1.0f // can be increased!
            };

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
        _timer.Dispose();
        _outputDevice?.Dispose();
        _audioFileReader?.Dispose();
    }

    public TimeSpan GetPlaybackPosition() =>
        _audioFileReader?.GetPosition() ?? TimeSpan.Zero;

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

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        _timer.Stop();

        _outputDevice?.Dispose();
        _outputDevice = null;

        _audioFileReader?.Dispose();
        _audioFileReader = null;

        OnMediaChangeEvent(CreateMediaEventArgs(_mediaItemId, MediaChange.Stopped));
    }

    private void OnMediaChangeEvent(MediaEventArgs e) =>
        MediaChangeEvent?.Invoke(this, e);

    private static MediaEventArgs CreateMediaEventArgs(Guid id, MediaChange change) =>
        new()
        {
            MediaItemId = id,
            Classification = MediaClassification.Audio,
            Change = change,
        };

    private void HandleTimerFire(object? sender, ElapsedEventArgs e)
    {
        if (!_manuallySettingPlaybackPosition)
        {
            MediaPositionChangedEvent?.Invoke(this, new OnlyMPositionChangedEventArgs(_mediaItemId, GetPlaybackPosition()));
        }
    }
}
