using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Newtonsoft.Json;
using OnlyM.Core.Models;
using OnlyM.Core.Utils;
using Serilog.Events;

namespace OnlyM.Core.Services.Options;

public sealed class Options
{
    public static readonly double MinMirrorZoom = 0.5;
    public static readonly double MaxMirrorZoom = 2.0;
    public static readonly double DefaultMirrorZoom = 1.0;

    private const int AbsoluteMaxItemCount = 200;
    private const int DefaultMaxItemCount = 50;

    private const double DefaultMagnifierZoomLevel = 0.5;
    private const double DefaultBrowserZoomLevelIncrement = 0.25;

    private const double MinBrowserFrameThickness = 0.0;
    private const double DefaultBrowserFrameThickness = 2.0;
    private const double MaxBrowserFrameThickness = 5.0;

    private const char DefaultMirrorHotKey = 'Z';

    public Options()
    {
        // defaults
        AlwaysOnTop = true;
        WindowedAlwaysOnTop = true;
        LogEventLevel = LogEventLevel.Information;
        MediaFolder = FileUtils.GetOnlyMDefaultMediaFolder();
        ImageFadeType = ImageFadeType.CrossFade;
        ImageFadeSpeed = FadeSpeed.Normal;
        CacheImages = true;
        ShowVideoSubtitles = false;
        AllowVideoScrubbing = true;
        AllowVideoPause = true;
        AllowVideoPositionSeeking = true;
        PermanentBackdrop = true;
        ConfirmVideoStop = false;
        MaxItemCount = DefaultMaxItemCount;
        MagnifierZoomLevel = DefaultMagnifierZoomLevel;
        BrowserZoomLevelIncrement = DefaultBrowserZoomLevelIncrement;
        MagnifierShape = MagnifierShape.Circle;
        MagnifierSize = MagnifierSize.Medium;
        MagnifierFrameThickness = DefaultBrowserFrameThickness;

        MediaWindowSize = Size.Empty;

        VideoScreenPosition = new ScreenPosition();
        ImageScreenPosition = new ScreenPosition();
        WebScreenPosition = new ScreenPosition();

        AllowMirror = true;
        MirrorZoom = DefaultMirrorZoom;
        SortMode = MediaSortMode.Auto;

        Sanitize();
    }

    public bool ShouldPurgeBrowserCacheOnStartup { get; set; }

    public int MetaDataParallelism { get; set; } = 3;

    public int RemoteControlPort { get; set; } = 8088;

    public bool RemoteControlEnabled { get; set; }

    public RemoteControlMode RemoteControlMode { get; set; } = RemoteControlMode.Local;

    /// <summary>
    /// Gets or sets the remote control PIN (4-8 digits, at least 6 in Cloudflare mode; empty for no PIN in Local mode).
    /// </summary>
    public string? RemoteControlPin { get; set; }

    /// <summary>
    /// Gets or sets the playback volume for video and audio (0 - 100).
    /// </summary>
    public int Volume { get; set; } = 100;

    public bool ShowMediaItemCommandPanel { get; set; }

    public const int MinRemoteControlPinLength = 4;

    public const int MaxRemoteControlPinLength = 8;

    /// <summary>
    /// Minimum PIN length when the remote is reachable from the internet (Cloudflare mode).
    /// </summary>
    public const int MinCloudflarePinLength = 6;

    public static bool IsValidRemoteControlPin(string? pin)
        => pin is { Length: >= MinRemoteControlPinLength and <= MaxRemoteControlPinLength } && pin.All(char.IsAsciiDigit);

    public static bool IsValidCloudflarePin(string? pin)
        => IsValidRemoteControlPin(pin) && pin!.Length >= MinCloudflarePinLength;

    public bool ShowFreezeCommand { get; set; }

    public bool ShowMediaItemCountBadge { get; set; }

    public string? MediaMonitorId { get; set; }

    public bool MediaWindowed { get; set; }

    public Size MediaWindowSize { get; set; }

    public RenderingMethod RenderingMethod { get; set; }

    [JsonIgnore]
    public DateTime OperatingDate { get; set; }

    public int MaxItemCount { get; set; }

    public ScreenPosition VideoScreenPosition { get; set; }

    public ScreenPosition ImageScreenPosition { get; set; }

    public ScreenPosition WebScreenPosition { get; set; }

    public bool IncludeBlankScreenItem { get; set; }

    public bool UseInternalMediaTitles { get; set; }

    public bool PermanentBackdrop { get; set; }

    public bool AllowVideoPause { get; set; }

    public bool AllowVideoPositionSeeking { get; set; }

    public bool ShowVideoSubtitles { get; set; }

    public bool AllowVideoScrubbing { get; set; }

    public bool AlwaysOnTop { get; set; }

    public bool WindowedAlwaysOnTop { get; set; }

    public string? AppWindowPlacement { get; set; }

    public string? MediaWindowPlacement { get; set; }

    public bool ConfirmVideoStop { get; set; }

    public string? Culture { get; set; }

    public LogEventLevel LogEventLevel { get; set; }

    public bool AutoRotateImages { get; set; }

    public string MediaFolder { get; set; }

    public ImageFadeType ImageFadeType { get; set; }

    public FadeSpeed ImageFadeSpeed { get; set; }

    public double BrowserZoomLevelIncrement { get; set; }

    public MagnifierShape MagnifierShape { get; set; }

    public MagnifierSize MagnifierSize { get; set; }

    public double MagnifierZoomLevel { get; set; }

    public double MagnifierFrameThickness { get; set; }

    public bool EmbeddedThumbnails { get; set; }

    public bool CacheImages { get; set; }

    public List<string> RecentlyUsedMediaFolders { get; set; } = [];

    public bool AllowMirror { get; set; }

    public bool UseMirrorByDefault { get; set; }

    public double MirrorZoom { get; set; }

    public char MirrorHotKey { get; set; }

    public DarkModeOption DarkModeOption { get; set; }

    public MediaSortMode SortMode { get; set; }

    public bool ShowSortModeToggleButton { get; set; }

    /// <summary>
    /// Validates the data, correcting automatically as required
    /// </summary>
    public void Sanitize()
    {
        if (!Directory.Exists(MediaFolder))
        {
            MediaFolder = FileUtils.GetOnlyMDefaultMediaFolder();
        }

        Volume = Math.Clamp(Volume, 0, 100);

        if (!string.IsNullOrEmpty(RemoteControlPin) && !IsValidRemoteControlPin(RemoteControlPin))
        {
            RemoteControlPin = null;
        }

        if (!System.Enum.IsDefined(RemoteControlMode))
        {
            RemoteControlMode = RemoteControlMode.Local;
        }

        if (RemoteControlPort is < 1 or > 65535)
        {
            RemoteControlPort = 8088;
        }

        VideoScreenPosition.Sanitize();
        ImageScreenPosition.Sanitize();
        WebScreenPosition.Sanitize();

        if (RecentlyUsedMediaFolders.Count == 0)
        {
            RecentlyUsedMediaFolders.Add(!string.IsNullOrEmpty(MediaFolder)
                ? MediaFolder
                : FileUtils.GetOnlyMDefaultMediaFolder());
        }

        for (var n = RecentlyUsedMediaFolders.Count - 1; n >= 0; --n)
        {
            var folder = RecentlyUsedMediaFolders[n];
            if (!Directory.Exists(folder))
            {
                RecentlyUsedMediaFolders.RemoveAt(n);
            }
        }

        // media calendar date is always set to today
        // on startup.
        OperatingDate = DateTime.Today;

        if (MaxItemCount > AbsoluteMaxItemCount)
        {
            MaxItemCount = AbsoluteMaxItemCount;
        }

        if (MaxItemCount <= 0)
        {
            MaxItemCount = 1;
        }

        if (MagnifierZoomLevel < 0 || MagnifierZoomLevel > 1.0)
        {
            MagnifierZoomLevel = DefaultMagnifierZoomLevel;
        }

        if (BrowserZoomLevelIncrement < 0 || BrowserZoomLevelIncrement > 1)
        {
            BrowserZoomLevelIncrement = DefaultBrowserZoomLevelIncrement;
        }

        if (MagnifierFrameThickness < MinBrowserFrameThickness ||
            MagnifierFrameThickness > MaxBrowserFrameThickness)
        {
            MagnifierFrameThickness = DefaultBrowserFrameThickness;
        }

        MirrorZoom = GetNormalisedMirrorZoom(MirrorZoom);

        if (MirrorHotKey < 'A' || MirrorHotKey > 'Z')
        {
            MirrorHotKey = DefaultMirrorHotKey;
        }

        if (!Enum.IsDefined(SortMode))
        {
            SortMode = MediaSortMode.Auto;
        }
    }

    public static double GetNormalisedMirrorZoom(double value)
    {
        if (value < Options.MinMirrorZoom)
        {
            return Options.MinMirrorZoom;
        }

        if (value > Options.MaxMirrorZoom)
        {
            return Options.MaxMirrorZoom;
        }

        var d = (int)Math.Round(value * 10);

        return d / 10.0;
    }
}
