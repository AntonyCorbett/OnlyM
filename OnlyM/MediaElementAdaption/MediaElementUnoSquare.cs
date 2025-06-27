using System;
using System.Threading.Tasks;
using System.Windows;
using OnlyM.Core.Models;
using OnlyM.Core.Utils;
using Serilog.Events;
using Unosquare.FFME.Common;

namespace OnlyM.MediaElementAdaption;

internal sealed class MediaElementUnoSquare : IMediaElement
{
    private readonly Unosquare.FFME.MediaElement _mediaElement;
    private TimeSpan _lastPositionChange;

    public MediaElementUnoSquare(Unosquare.FFME.MediaElement mediaElement)
    {
        _mediaElement = mediaElement;
        _mediaElement.Volume = 1.0; // max = 1.0

        _mediaElement.MediaOpened += HandleMediaOpened;
        _mediaElement.MediaClosed += HandleMediaClosed;
        _mediaElement.MediaEnded += HandleMediaEnded;
        _mediaElement.MediaFailed += HandleMediaFailed;
        _mediaElement.RenderingSubtitles += HandleRenderingSubtitles;
        _mediaElement.PositionChanged += HandlePositionChanged;
        _mediaElement.MessageLogged += HandleMessageLogged;
    }

    public event EventHandler<OnlyMMediaOpenedEventArgs>? MediaOpened;

    public event EventHandler<OnlyMMediaClosedEventArgs>? MediaClosed;

    public event EventHandler<OnlyMMediaEndedEventArgs>? MediaEnded;

    public event EventHandler<OnlyMMediaFailedEventArgs>? MediaFailed;

    public event EventHandler<OnlyMRenderSubtitlesEventArgs>? RenderingSubtitles;

    public event EventHandler<OnlyMPositionChangedEventArgs>? PositionChanged;

    public event EventHandler<OnlyMLogMessageEventArgs>? MessageLogged;

    public TimeSpan Position
    {
        get => _mediaElement.Position;
        set => _mediaElement.Position = value;
    }

    public Duration NaturalDuration => new(_mediaElement.NaturalDuration ?? TimeSpan.Zero);

    public FrameworkElement FrameworkElement => _mediaElement;

    public Guid MediaItemId { get; set; }

    public bool IsPaused { get; private set; }

    public async Task Play(Uri mediaPath, MediaClassification mediaClassification)
    {
        _lastPositionChange = TimeSpan.Zero;

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

    private async void HandleMediaOpened(object? sender, MediaOpenedEventArgs e)
    {
        await _mediaElement.Play();
        MediaOpened?.Invoke(sender, new OnlyMMediaOpenedEventArgs());
    }

    private void HandleMediaClosed(object? sender, EventArgs e) =>
        MediaClosed?.Invoke(sender, new OnlyMMediaClosedEventArgs());

    private void HandleMediaEnded(object? sender, EventArgs e) =>
        MediaEnded?.Invoke(sender, new OnlyMMediaEndedEventArgs());

    private void HandleMediaFailed(object? sender, MediaFailedEventArgs e) =>
        MediaFailed?.Invoke(sender, new OnlyMMediaFailedEventArgs { Error = e.ErrorException });

    private void HandleRenderingSubtitles(object? sender, RenderingSubtitlesEventArgs e)
    {
        var args = new OnlyMRenderSubtitlesEventArgs { Cancel = e.Cancel };
        RenderingSubtitles?.Invoke(sender, args);
        e.Cancel = args.Cancel;
    }

    private void HandlePositionChanged(object? sender, PositionChangedEventArgs e)
    {
        if ((e.Position - _lastPositionChange) < IMediaElement.PositionChangedInterval)
        {
            // Avoid flooding with position change events
            return;
        }

        _lastPositionChange = e.Position;
        PositionChanged?.Invoke(sender, new OnlyMPositionChangedEventArgs(MediaItemId, e.Position));
    }

    private void HandleMessageLogged(object? sender, MediaLogMessageEventArgs e)
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

        MessageLogged?.Invoke(sender, new OnlyMLogMessageEventArgs(level, e.Message));
    }
}
