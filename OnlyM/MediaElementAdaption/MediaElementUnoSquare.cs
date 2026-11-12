using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using FFmpeg.AutoGen;
using OnlyM.Core.Models;
using OnlyM.Core.Services.Options;
using OnlyM.Core.Utils;
using OnlyM.EventTracking;
using Serilog.Events;
using Unosquare.FFME.Common;

namespace OnlyM.MediaElementAdaption;

internal sealed class MediaElementUnoSquare : IMediaElement
{
    private readonly Unosquare.FFME.MediaElement _mediaElement;
    private readonly IOptionsService _optionsService;

    // media that failed while hardware decoding was in use; these are only
    // decoded in software for the rest of the session.
    private readonly HashSet<Uri> _softwareOnlySources = [];

    private TimeSpan _lastPositionChange;
    private Uri? _currentSource;
    private bool _hardwareDecodingRequested;
    private TimeSpan _resumePosition;

    public MediaElementUnoSquare(Unosquare.FFME.MediaElement mediaElement, IOptionsService optionsService)
    {
        _mediaElement = mediaElement;
        _optionsService = optionsService;
        _mediaElement.Volume = 1.0; // max = 1.0

        _mediaElement.MediaOpening += HandleMediaOpening;
        _mediaElement.MediaOpened += HandleMediaOpened;
        _mediaElement.MediaClosed += HandleMediaClosed;
        _mediaElement.MediaEnded += HandleMediaEnded;
        _mediaElement.MediaFailed += HandleMediaFailed;
        _mediaElement.RenderingSubtitles += HandleRenderingSubtitles;
        _mediaElement.PositionChanged += HandlePositionChanged;
        _mediaElement.MessageLogged += HandleMessageLogged;
    }

    public double Volume
    {
        get => _mediaElement.Volume;
        set => _mediaElement.Volume = value;
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
        _resumePosition = TimeSpan.Zero;

        IsPaused = false;

        mediaPath = FFmpegUtils.FixUnicodeUri(mediaPath);
        _currentSource = mediaPath;

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
        _resumePosition = TimeSpan.Zero;
        await _mediaElement.Close();
    }

    public void UnsubscribeEvents()
    {
        _mediaElement.MediaOpening -= HandleMediaOpening;
        _mediaElement.MediaOpened -= HandleMediaOpened;
        _mediaElement.MediaClosed -= HandleMediaClosed;
        _mediaElement.MediaEnded -= HandleMediaEnded;
        _mediaElement.MediaFailed -= HandleMediaFailed;
        _mediaElement.RenderingSubtitles -= HandleRenderingSubtitles;
        _mediaElement.PositionChanged -= HandlePositionChanged;
        _mediaElement.MessageLogged -= HandleMessageLogged;
    }

    private static HardwareDeviceInfo? SelectHardwareDevice(IReadOnlyList<HardwareDeviceInfo>? devices)
    {
        // The list contains the devices the FFmpeg build supports for this codec, not
        // necessarily what this PC supports. If the device can't be created, or the GPU can't
        // decode this format, FFME/FFmpeg fall back to software decoding.
        // D3D11VA is preferred (Windows 10+); DXVA2 is used only if D3D11VA isn't offered.
        // CUDA (unsupported for 32-bit apps by current NVIDIA drivers) and QSV (Intel only)
        // are deliberately not used.
        if (devices == null)
        {
            return null;
        }

        return devices.FirstOrDefault(d => d.DeviceType == AVHWDeviceType.AV_HWDEVICE_TYPE_D3D11VA)
               ?? devices.FirstOrDefault(d => d.DeviceType == AVHWDeviceType.AV_HWDEVICE_TYPE_DXVA2);
    }

    // set when OnlyM runs with --nogpu or has no usable GPU (WPF rendering tier 0)
    private static bool IsSoftwareRenderingForced() =>
        RenderOptions.ProcessRenderMode == RenderMode.SoftwareOnly;

    private void HandleMediaOpening(object? sender, MediaOpeningEventArgs e)
    {
        _hardwareDecodingRequested = false;

        try
        {
            var videoStream = e.Options.VideoStream;
            if (videoStream == null ||
                !_optionsService.UseHardwareVideoDecoding ||
                IsSoftwareRenderingForced() ||
                (_currentSource != null && _softwareOnlySources.Contains(_currentSource)))
            {
                return;
            }

            var device = SelectHardwareDevice(videoStream.HardwareDevices);
            if (device == null)
            {
                Serilog.Log.Logger.Debug("No supported hardware decoder for codec {Codec}", videoStream.CodecName);
                return;
            }

            e.Options.VideoHardwareDevice = device;
            _hardwareDecodingRequested = true;
        }
        catch (Exception ex)
        {
            // never let hardware selection prevent playback
            e.Options.VideoHardwareDevice = null;
            _hardwareDecodingRequested = false;
            Serilog.Log.Logger.Error(ex, "Selecting hardware video decoder");
        }
    }

    private async void HandleMediaOpened(object? sender, MediaOpenedEventArgs e)
    {
        try
        {
            if (!string.IsNullOrEmpty(_mediaElement.VideoCodec))
            {
                Serilog.Log.Logger.Information(
                    "Video opened. Codec = {Codec}, hardware decoder = {HardwareDecoder}",
                    _mediaElement.VideoCodec,
                    string.IsNullOrEmpty(_mediaElement.VideoHardwareDecoder) ? "none" : _mediaElement.VideoHardwareDecoder);
            }

            await _mediaElement.Play();
            MediaOpened?.Invoke(sender, new OnlyMMediaOpenedEventArgs());

            if (_resumePosition > TimeSpan.Zero)
            {
                // reopened in software after a hardware decoding failure mid-playback
                _mediaElement.Position = _resumePosition;
                _resumePosition = TimeSpan.Zero;
            }
        }
        catch (Exception ex)
        {
            EventTracker.Error(ex, "Opening media");
            Serilog.Log.Logger.Error(ex, "HandleMediaOpen");
        }
    }

    private void HandleMediaClosed(object? sender, EventArgs e) =>
        MediaClosed?.Invoke(sender, new OnlyMMediaClosedEventArgs());

    private void HandleMediaEnded(object? sender, EventArgs e) =>
        MediaEnded?.Invoke(sender, new OnlyMMediaEndedEventArgs());

    private void HandleMediaFailed(object? sender, MediaFailedEventArgs e)
    {
        var source = _currentSource;

        if (_hardwareDecodingRequested && source != null && _softwareOnlySources.Add(source))
        {
            // The failure may be caused by the GPU or its driver, so try once more in software.
            _hardwareDecodingRequested = false;
            _resumePosition = _lastPositionChange;

            EventTracker.Error(e.ErrorException, "Hardware video decoding failed; retrying in software");
            Serilog.Log.Logger.Warning(
                e.ErrorException,
                "Media failed with hardware decoding ({HardwareDecoder}); retrying with software decoding",
                _mediaElement.VideoHardwareDecoder);

            // let FFME finish handling the failure before reopening
            _ = _mediaElement.Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    await _mediaElement.Open(source);
                }
                catch (Exception ex)
                {
                    Serilog.Log.Logger.Error(ex, "Retrying media with software decoding");
                    MediaFailed?.Invoke(sender, new OnlyMMediaFailedEventArgs { Error = ex });
                }
            });

            return;
        }

        MediaFailed?.Invoke(sender, new OnlyMMediaFailedEventArgs { Error = e.ErrorException });
    }

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
