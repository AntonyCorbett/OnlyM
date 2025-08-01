﻿using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.Messaging;
using OnlyM.Core.Models;
using OnlyM.Core.Services.Options;
using OnlyM.Core.Subtitles;
using OnlyM.EventTracking;
using OnlyM.MediaElementAdaption;
using OnlyM.Models;
using OnlyM.PubSubMessages;
using Serilog;
using Serilog.Events;

namespace OnlyM.Services;

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
    private SubtitleProvider? _subTitleProvider;
    private string? _mediaItemFilePath;

    public VideoDisplayManager(IMediaElement mediaElement, TextBlock subtitleBlock, IOptionsService optionsService)
    {
        _mediaElement = mediaElement;
        _subtitleBlock = subtitleBlock;

        _optionsService = optionsService;

        SubscribeEvents();
    }

    public event EventHandler<MediaEventArgs>? MediaChangeEvent;

    public event EventHandler<OnlyMPositionChangedEventArgs>? MediaPositionChangedEvent;

    public event EventHandler<MediaNearEndEventArgs>? MediaNearEndEvent;

    public event EventHandler<SubtitleEventArgs>? SubtitleEvent;

    public bool ShowSubtitles { get; set; }

    public bool IsPaused => _mediaElement.IsPaused;

    public void Dispose() => UnsubscribeEvents();

    public async Task ShowVideoAsync(
        string mediaItemFilePath,
        ScreenPosition screenPosition,
        Guid mediaItemId,
        TimeSpan startOffset,
        bool startFromPaused)
    {
        _mediaItemId = mediaItemId;
        _mediaItemFilePath = mediaItemFilePath;

        Log.Debug("ShowVideo - Media Id = {MediaItemId}", _mediaItemId);

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
            Log.Debug("Firing Started - Media Id = {MediaItemId}", _mediaItemId);

            await CreateSubtitleFile(mediaItemFilePath);

            await _mediaElement.Play(new Uri(mediaItemFilePath), MediaClassification.Video);
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

    public TimeSpan GetPlaybackPosition() => _mediaElement.Position;

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

    private void OnMediaChangeEvent(MediaEventArgs e) => MediaChangeEvent?.Invoke(this, e);

    private async void HandleMediaOpened(object? sender, OnlyMMediaOpenedEventArgs e)
    {
        try
        {
            Log.Logger.Information("Opened");

            _firedNearEndEvent = false;

            _mediaElement.Position = _startPosition;
            OnMediaChangeEvent(CreateMediaEventArgs(_mediaItemId, MediaChange.Started));

            await CreateSubtitleProvider(_mediaItemFilePath, _startPosition);
        }
        catch (Exception ex)
        {
            EventTracker.Error(ex, "Opening media");
            Log.Logger.Error(ex, "Error opening media");
        }
    }

    private void HandleMediaClosed(object? sender, OnlyMMediaClosedEventArgs e)
    {
        Log.Logger.Debug("Media closed");

        _firedNearEndEvent = false;

        OnMediaChangeEvent(CreateMediaEventArgs(_mediaItemId, MediaChange.Stopped));
    }

    private async void HandleMediaEnded(object? sender, OnlyMMediaEndedEventArgs e)
    {
        try
        {
            Log.Logger.Debug("Media ended");

            if (!_mediaElement.IsPaused)
            {
                OnMediaChangeEvent(CreateMediaEventArgs(_mediaItemId, MediaChange.Stopped));
                await _mediaElement.Close();
            }
        }
        catch (Exception ex)
        {
            EventTracker.Error(ex, "Handling media ended");
            Log.Logger.Error(ex, "Error handling media ended");
        }
    }

    private void HandleMediaFailed(object? sender, OnlyMMediaFailedEventArgs e)
    {
        Log.Logger.Debug("Media failed");
        OnMediaChangeEvent(CreateMediaEventArgs(_mediaItemId, MediaChange.Stopped));
    }

    private void HandlePositionChanged(object? sender, OnlyMPositionChangedEventArgs e)
    {
        if (!_manuallySettingPlaybackPosition && (e.Position - _lastPosition).TotalMilliseconds > 60)
        {
            // only fire every 60ms
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

    private void HandleRenderingSubtitles(object? sender, OnlyMRenderSubtitlesEventArgs e) =>
        e.Cancel = !ShowSubtitles;

    private void HandleMediaElementMessageLogged(object? sender, OnlyMLogMessageEventArgs e)
    {
        switch (e.Level)
        {
            case LogEventLevel.Debug:
                Log.Logger.Debug("{LogMessage}", e.Message);
                break;

            case LogEventLevel.Error:
                Log.Logger.Error("{LogMessage}", e.Message);
                break;

            case LogEventLevel.Information:
                Log.Logger.Information("{LogMessage}", e.Message);
                break;

            case LogEventLevel.Verbose:
                Log.Logger.Verbose("{LogMessage}", e.Message);
                break;

            case LogEventLevel.Warning:
                Log.Logger.Warning("{LogMessage}", e.Message);
                break;
        }
    }

    private static MediaEventArgs CreateMediaEventArgs(Guid id, MediaChange change) =>
        new()
        {
            MediaItemId = id,
            Classification = MediaClassification.Video,
            Change = change,
        };

    private void HandleSubtitleEvent(object? sender, SubtitleEventArgs e)
    {
        // only used in MediaFoundation as the other engines have their own
        // internal subtitle processing...
        if (e.Status == SubtitleStatus.NotShowing || ShowSubtitles)
        {
            SubtitleEvent?.Invoke(sender, e);
        }
    }

    private Task<string?> CreateSubtitleFile(string mediaItemFilePath) =>
        Task.Run(() =>
        {
            if (_mediaElement is MediaElementMediaFoundation &&
                _optionsService.ShowVideoSubtitles)
            {
                return SubtitleFileGenerator.Generate(mediaItemFilePath, _mediaItemId);
            }

            return null;
        });

    private async Task CreateSubtitleProvider(string? mediaItemFilePath, TimeSpan videoHeadPosition)
    {
        if (_subTitleProvider != null)
        {
            _subTitleProvider.SubtitleEvent -= HandleSubtitleEvent;
            _subTitleProvider = null;
        }

        if (_mediaElement is MediaElementMediaFoundation &&
            mediaItemFilePath != null &&
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

    private void HandleSubtitleFileEvent(object? sender, SubtitleFileEventArgs e) =>
        WeakReferenceMessenger.Default.Send(new SubtitleFileMessage { MediaItemId = e.MediaItemId, Starting = e.Starting });

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
