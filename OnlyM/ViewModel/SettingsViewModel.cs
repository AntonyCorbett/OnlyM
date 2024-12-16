using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.WindowsAPICodePack.Dialogs;
using OnlyM.AutoUpdates;
using OnlyM.Core.Extensions;
using OnlyM.Core.Models;
using OnlyM.Core.Services.Media;
using OnlyM.Core.Services.Monitors;
using OnlyM.Core.Services.Options;
using OnlyM.CoreSys.Services.Snackbar;
using OnlyM.Models;
using OnlyM.PubSubMessages;
using OnlyM.Services;
using OnlyM.Services.MediaChanging;
using OnlyM.Services.Pages;
using Serilog.Events;

using Size = System.Windows.Size;

namespace OnlyM.ViewModel;

// ReSharper disable once UnusedMember.Global
internal sealed class SettingsViewModel : ObservableObject
{
    private static readonly Size Size360P = new(640, 360);
    private static readonly Size Size480P = new(854, 480);
    private static readonly Size Size720P = new(1280, 720);
    private static readonly Size Size1080P = new(1920, 1080);

    private static readonly Size MinSize = new(192, 108);
    private static readonly Size MaxSize = new(8192, 6144);

    private readonly IPageService _pageService;
    private readonly IMonitorsService _monitorsService;
    private readonly IOptionsService _optionsService;
    private readonly IThumbnailService _thumbnailService;
    private readonly IActiveMediaItemsService _activeMediaItemsService;
    private readonly ISnackbarService _snackbarService;
    private readonly MonitorItem[] _monitors;
    private readonly MirrorHotKeyItem[] _mirrorHotKeys;
    private readonly LanguageItem[] _languages;
    private readonly RenderingMethodItem[] _renderingMethods;
    private readonly LoggingLevel[] _loggingLevels;
    private readonly ImageFade[] _fadingTypes;
    private readonly ImageFadeSpeed[] _fadingSpeeds;
    private readonly RecentlyUsedFolders _recentlyUsedMediaFolders;
    private readonly MagnifierShapeItem[] _magnifierShapes;
    private readonly MagnifierSizeItem[] _magnifierSizes;

    private bool _isMediaActive;

    public SettingsViewModel(
        IPageService pageService,
        IMonitorsService monitorsService,
        IOptionsService optionsService,
        IActiveMediaItemsService activeMediaItemsService,
        IThumbnailService thumbnailService,
        ISnackbarService snackbarService)
    {
        _pageService = pageService;
        _monitorsService = monitorsService;
        _optionsService = optionsService;
        _thumbnailService = thumbnailService;
        _activeMediaItemsService = activeMediaItemsService;
        _snackbarService = snackbarService;

        _recentlyUsedMediaFolders = new RecentlyUsedFolders();
        InitRecentlyUsedFolders();

        _monitors = GetSystemMonitors();
        _languages = GetSupportedLanguages();
        _loggingLevels = GetLoggingLevels();
        _fadingTypes = GetImageFadingTypes();
        _fadingSpeeds = GetFadingSpeedTypes();
        _renderingMethods = GetRenderingMethods();
        _magnifierShapes = GetMagnifierShapes();
        _magnifierSizes = GetMagnifierSizes();
        _mirrorHotKeys = GetMirrorHotKeys();

        _pageService.NavigationEvent += HandleNavigationEvent;

        InitCommands();
        WeakReferenceMessenger.Default.Register<ShutDownMessage>(this, OnShutDown);
    }

    public RelayCommand Set360PSizeCommand { get; set; } = null!;

    public RelayCommand Set480PSizeCommand { get; set; } = null!;

    public RelayCommand Set720PSizeCommand { get; set; } = null!;

    public RelayCommand Set1080PSizeCommand { get; set; } = null!;

    public RelayCommand PurgeThumbnailCacheCommand { get; set; } = null!;

    public RelayCommand PurgeWebCacheCommand { get; set; } = null!;

    public RelayCommand OpenMediaFolderCommand { get; set; } = null!;

    public ObservableCollection<string> RecentMediaFolders => _recentlyUsedMediaFolders.GetFolders();

#pragma warning disable CA1863
    public static string AppVersionStr => string.Format(CultureInfo.CurrentCulture, Properties.Resources.APP_VER, VersionDetection.GetCurrentVersion());
#pragma warning restore CA1863

    public IEnumerable<ImageFadeSpeed> FadeSpeedTypes => _fadingSpeeds;

    public IEnumerable<ImageFade> ImageFadeTypes => _fadingTypes;

    public IEnumerable<MagnifierShapeItem> MagnifierShapes => _magnifierShapes;

    public IEnumerable<MagnifierSizeItem> MagnifierSizes => _magnifierSizes;

    public IEnumerable<MirrorHotKeyItem> MirrorHotKeys => _mirrorHotKeys;

    public bool IsBrowserCachePurgeQueued => _optionsService.ShouldPurgeBrowserCacheOnStartup;

    public string MaxItemCount
    {
        get => _optionsService.MaxItemCount.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (!string.IsNullOrEmpty(value) &&
                !_optionsService.MaxItemCount.ToString(CultureInfo.InvariantCulture).Equals(value, StringComparison.Ordinal) &&
                int.TryParse(value, out var count))
            {
                _optionsService.MaxItemCount = count;
                OnPropertyChanged();
            }
        }
    }

    public DateTime MediaCalendarDate
    {
        get => _optionsService.OperatingDate;
        set
        {
            if (_optionsService.OperatingDate != value)
            {
                _optionsService.OperatingDate = value;
                OnPropertyChanged();
            }
        }
    }

    public bool AlwaysOnTop
    {
        get => _optionsService.AlwaysOnTop;
        set
        {
            if (_optionsService.AlwaysOnTop != value)
            {
                _optionsService.AlwaysOnTop = value;
                OnPropertyChanged();
            }
        }
    }

    public int VideoScreenLeftMargin
    {
        get => _optionsService.VideoScreenPosition.LeftMarginPercentage;
        set
        {
            if (_optionsService.VideoScreenPosition.LeftMarginPercentage != value)
            {
                var newPos = (ScreenPosition)_optionsService.VideoScreenPosition.Clone();
                ScreenPositionHelper.ModifyScreenPosition(newPos, ScreenMarginSide.Left, value, out var opposingMarginChanged);

                _optionsService.VideoScreenPosition = newPos;
                OnPropertyChanged();

                if (opposingMarginChanged)
                {
                    OnPropertyChanged(nameof(VideoScreenRightMargin));
                }
            }
        }
    }

    public int VideoScreenRightMargin
    {
        get => _optionsService.VideoScreenPosition.RightMarginPercentage;
        set
        {
            if (_optionsService.VideoScreenPosition.RightMarginPercentage != value)
            {
                var newPos = (ScreenPosition)_optionsService.VideoScreenPosition.Clone();
                ScreenPositionHelper.ModifyScreenPosition(newPos, ScreenMarginSide.Right, value, out var opposingMarginChanged);

                _optionsService.VideoScreenPosition = newPos;
                OnPropertyChanged();

                if (opposingMarginChanged)
                {
                    OnPropertyChanged(nameof(VideoScreenLeftMargin));
                }
            }
        }
    }

    public int VideoScreenTopMargin
    {
        get => _optionsService.VideoScreenPosition.TopMarginPercentage;
        set
        {
            if (_optionsService.VideoScreenPosition.TopMarginPercentage != value)
            {
                var newPos = (ScreenPosition)_optionsService.VideoScreenPosition.Clone();
                ScreenPositionHelper.ModifyScreenPosition(newPos, ScreenMarginSide.Top, value, out var opposingMarginChanged);

                _optionsService.VideoScreenPosition = newPos;
                OnPropertyChanged();

                if (opposingMarginChanged)
                {
                    OnPropertyChanged(nameof(VideoScreenBottomMargin));
                }
            }
        }
    }

    public int VideoScreenBottomMargin
    {
        get => _optionsService.VideoScreenPosition.BottomMarginPercentage;
        set
        {
            if (_optionsService.VideoScreenPosition.BottomMarginPercentage != value)
            {
                var newPos = (ScreenPosition)_optionsService.VideoScreenPosition.Clone();
                ScreenPositionHelper.ModifyScreenPosition(newPos, ScreenMarginSide.Bottom, value, out var opposingMarginChanged);

                _optionsService.VideoScreenPosition = newPos;
                OnPropertyChanged();

                if (opposingMarginChanged)
                {
                    OnPropertyChanged(nameof(VideoScreenTopMargin));
                }
            }
        }
    }

    public int ImageScreenLeftMargin
    {
        get => _optionsService.ImageScreenPosition.LeftMarginPercentage;
        set
        {
            if (_optionsService.ImageScreenPosition.LeftMarginPercentage != value)
            {
                var newPos = (ScreenPosition)_optionsService.ImageScreenPosition.Clone();
                ScreenPositionHelper.ModifyScreenPosition(newPos, ScreenMarginSide.Left, value, out var opposingMarginChanged);

                _optionsService.ImageScreenPosition = newPos;
                OnPropertyChanged();

                if (opposingMarginChanged)
                {
                    OnPropertyChanged(nameof(ImageScreenRightMargin));
                }
            }
        }
    }

    public int ImageScreenRightMargin
    {
        get => _optionsService.ImageScreenPosition.RightMarginPercentage;
        set
        {
            if (_optionsService.ImageScreenPosition.RightMarginPercentage != value)
            {
                var newPos = (ScreenPosition)_optionsService.ImageScreenPosition.Clone();
                ScreenPositionHelper.ModifyScreenPosition(newPos, ScreenMarginSide.Right, value, out var opposingMarginChanged);

                _optionsService.ImageScreenPosition = newPos;
                OnPropertyChanged();

                if (opposingMarginChanged)
                {
                    OnPropertyChanged(nameof(ImageScreenLeftMargin));
                }
            }
        }
    }

    public int ImageScreenTopMargin
    {
        get => _optionsService.ImageScreenPosition.TopMarginPercentage;
        set
        {
            if (_optionsService.ImageScreenPosition.TopMarginPercentage != value)
            {
                var newPos = (ScreenPosition)_optionsService.ImageScreenPosition.Clone();
                ScreenPositionHelper.ModifyScreenPosition(newPos, ScreenMarginSide.Top, value, out var opposingMarginChanged);

                _optionsService.ImageScreenPosition = newPos;
                OnPropertyChanged();

                if (opposingMarginChanged)
                {
                    OnPropertyChanged(nameof(ImageScreenBottomMargin));
                }
            }
        }
    }

    public int ImageScreenBottomMargin
    {
        get => _optionsService.ImageScreenPosition.BottomMarginPercentage;
        set
        {
            if (_optionsService.ImageScreenPosition.BottomMarginPercentage != value)
            {
                var newPos = (ScreenPosition)_optionsService.ImageScreenPosition.Clone();
                ScreenPositionHelper.ModifyScreenPosition(newPos, ScreenMarginSide.Bottom, value, out var opposingMarginChanged);

                _optionsService.ImageScreenPosition = newPos;
                OnPropertyChanged();

                if (opposingMarginChanged)
                {
                    OnPropertyChanged(nameof(ImageScreenTopMargin));
                }
            }
        }
    }

    public int WebScreenLeftMargin
    {
        get => _optionsService.WebScreenPosition.LeftMarginPercentage;
        set
        {
            if (_optionsService.WebScreenPosition.LeftMarginPercentage != value)
            {
                var newPos = (ScreenPosition)_optionsService.WebScreenPosition.Clone();
                ScreenPositionHelper.ModifyScreenPosition(newPos, ScreenMarginSide.Left, value, out var opposingMarginChanged);

                _optionsService.WebScreenPosition = newPos;
                OnPropertyChanged();

                if (opposingMarginChanged)
                {
                    OnPropertyChanged(nameof(WebScreenRightMargin));
                }
            }
        }
    }

    public int WebScreenRightMargin
    {
        get => _optionsService.WebScreenPosition.RightMarginPercentage;
        set
        {
            if (_optionsService.WebScreenPosition.RightMarginPercentage != value)
            {
                var newPos = (ScreenPosition)_optionsService.WebScreenPosition.Clone();
                ScreenPositionHelper.ModifyScreenPosition(newPos, ScreenMarginSide.Right, value, out var opposingMarginChanged);

                _optionsService.WebScreenPosition = newPos;
                OnPropertyChanged();

                if (opposingMarginChanged)
                {
                    OnPropertyChanged(nameof(WebScreenLeftMargin));
                }
            }
        }
    }

    public int WebScreenTopMargin
    {
        get => _optionsService.WebScreenPosition.TopMarginPercentage;
        set
        {
            if (_optionsService.WebScreenPosition.TopMarginPercentage != value)
            {
                var newPos = (ScreenPosition)_optionsService.WebScreenPosition.Clone();
                ScreenPositionHelper.ModifyScreenPosition(newPos, ScreenMarginSide.Top, value, out var opposingMarginChanged);

                _optionsService.WebScreenPosition = newPos;
                OnPropertyChanged();

                if (opposingMarginChanged)
                {
                    OnPropertyChanged(nameof(WebScreenBottomMargin));
                }
            }
        }
    }

    public int WebScreenBottomMargin
    {
        get => _optionsService.WebScreenPosition.BottomMarginPercentage;
        set
        {
            if (_optionsService.WebScreenPosition.BottomMarginPercentage != value)
            {
                var newPos = (ScreenPosition)_optionsService.WebScreenPosition.Clone();
                ScreenPositionHelper.ModifyScreenPosition(newPos, ScreenMarginSide.Bottom, value, out var opposingMarginChanged);

                _optionsService.WebScreenPosition = newPos;
                OnPropertyChanged();

                if (opposingMarginChanged)
                {
                    OnPropertyChanged(nameof(WebScreenTopMargin));
                }
            }
        }
    }

    public bool AllowMirror
    {
        get => _optionsService.AllowMirror;
        set
        {
            if (_optionsService.AllowMirror != value)
            {
                _optionsService.AllowMirror = value;
                OnPropertyChanged();
            }
        }
    }

    public bool MirrorByDefault
    {
        get => _optionsService.UseMirrorByDefault;
        set
        {
            if (_optionsService.UseMirrorByDefault != value)
            {
                _optionsService.UseMirrorByDefault = value;
                OnPropertyChanged();
            }
        }
    }

    public double MirrorZoom
    {
        get => _optionsService.MirrorZoom;
        set
        {
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (_optionsService.MirrorZoom != value)
            {
                _optionsService.MirrorZoom = value;
                OnPropertyChanged();
            }
        }
    }

    public char MirrorHotKey
    {
        get => _optionsService.MirrorHotKey;
        set
        {
            if (_optionsService.MirrorHotKey != value)
            {
                _optionsService.MirrorHotKey = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ShowCommandPanel
    {
        get => _optionsService.ShowMediaItemCommandPanel;
        set
        {
            if (_optionsService.ShowMediaItemCommandPanel != value)
            {
                _optionsService.ShowMediaItemCommandPanel = value;
                OnPropertyChanged();
            }
        }
    }

    public bool UseInternalMediaTitles
    {
        get => _optionsService.UseInternalMediaTitles;
        set
        {
            if (_optionsService.UseInternalMediaTitles != value)
            {
                _optionsService.UseInternalMediaTitles = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IncludeBlankScreenItem
    {
        get => _optionsService.IncludeBlankScreenItem;
        set
        {
            if (_optionsService.IncludeBlankScreenItem != value)
            {
                _optionsService.IncludeBlankScreenItem = value;
                OnPropertyChanged();
            }
        }
    }

    public bool PermanentBackdrop
    {
        get => _optionsService.PermanentBackdrop;
        set
        {
            if (_optionsService.PermanentBackdrop != value)
            {
                _optionsService.PermanentBackdrop = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(NotPermanentBackdrop));
            }
        }
    }

    public bool NotPermanentBackdrop => !PermanentBackdrop;

    public bool ShowVideoSubtitles
    {
        get => _optionsService.ShowVideoSubtitles;
        set
        {
            if (_optionsService.ShowVideoSubtitles != value)
            {
                _optionsService.ShowVideoSubtitles = value;
                OnPropertyChanged();
            }
        }
    }

    public bool AllowVideoScrubbing
    {
        get => _optionsService.AllowVideoScrubbing;
        set
        {
            if (_optionsService.AllowVideoScrubbing != value)
            {
                _optionsService.AllowVideoScrubbing = value;
                OnPropertyChanged();
            }
        }
    }

    public bool AllowVideoPositionSeeking
    {
        get => _optionsService.AllowVideoPositionSeeking;
        set
        {
            if (_optionsService.AllowVideoPositionSeeking != value)
            {
                _optionsService.AllowVideoPositionSeeking = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ConfirmWhenStoppingVideo
    {
        get => _optionsService.ConfirmVideoStop;
        set
        {
            if (_optionsService.ConfirmVideoStop != value)
            {
                _optionsService.ConfirmVideoStop = value;
                OnPropertyChanged();
            }
        }
    }

    public bool AllowVideoPause
    {
        get => _optionsService.AllowVideoPause;
        set
        {
            if (_optionsService.AllowVideoPause != value)
            {
                _optionsService.AllowVideoPause = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ShowFreezeCommand
    {
        get => _optionsService.ShowFreezeCommand;
        set
        {
            if (_optionsService.ShowFreezeCommand != value)
            {
                _optionsService.ShowFreezeCommand = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsMediaActive
    {
        get => _isMediaActive;
        set
        {
            if (_isMediaActive != value)
            {
                _isMediaActive = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsMediaInactive));
            }
        }
    }

    public bool IsMediaFolderOverriden => _optionsService.IsCommandLineMediaFolderSpecified();

    public bool IsMediaInactive => !IsMediaActive;

    public bool EmbeddedThumbnails
    {
        get => _optionsService.EmbeddedThumbnails;
        set
        {
            if (_optionsService.EmbeddedThumbnails != value)
            {
                _optionsService.EmbeddedThumbnails = value;
                OnPropertyChanged();
                PurgeThumbnailCache();
            }
        }
    }

    public bool CacheImages
    {
        get => _optionsService.CacheImages;
        set
        {
            if (_optionsService.CacheImages != value)
            {
                _optionsService.CacheImages = value;
                OnPropertyChanged();
            }
        }
    }

    public bool AutoRotateImages
    {
        get => _optionsService.AutoRotateImages;
        set
        {
            if (_optionsService.AutoRotateImages != value)
            {
                _optionsService.AutoRotateImages = value;
                OnPropertyChanged();
            }
        }
    }

    public string MediaFolder
    {
        get => _optionsService.MediaFolder;
        set
        {
            if (_optionsService.MediaFolder != value)
            {
                _optionsService.MediaFolder = value;
                OnPropertyChanged();
            }
        }
    }

    public ImageFadeType ImageFadeType
    {
        get => _optionsService.ImageFadeType;
        set
        {
            if (_optionsService.ImageFadeType != value)
            {
                _optionsService.ImageFadeType = value;
                OnPropertyChanged();
            }
        }
    }

    public MagnifierShape MagnifierShape
    {
        get => _optionsService.MagnifierShape;
        set
        {
            if (_optionsService.MagnifierShape != value)
            {
                _optionsService.MagnifierShape = value;
                OnPropertyChanged();
            }
        }
    }

    public MagnifierSize MagnifierSize
    {
        get => _optionsService.MagnifierSize;
        set
        {
            if (_optionsService.MagnifierSize != value)
            {
                _optionsService.MagnifierSize = value;
                OnPropertyChanged();
            }
        }
    }

    public double MagnifierZoomLevel
    {
        get => _optionsService.MagnifierZoomLevel;
        set
        {
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (_optionsService.MagnifierZoomLevel != value)
            {
                _optionsService.MagnifierZoomLevel = value;
                OnPropertyChanged();
            }
        }
    }

    public double MagnifierFrameThickness
    {
        get => _optionsService.MagnifierFrameThickness;
        set
        {
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (_optionsService.MagnifierFrameThickness != value)
            {
                _optionsService.MagnifierFrameThickness = value;
                OnPropertyChanged();
            }
        }
    }

    public double WebPageZoomIncrement
    {
        get => _optionsService.BrowserZoomLevelIncrement;
        set
        {
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (_optionsService.BrowserZoomLevelIncrement != value)
            {
                _optionsService.BrowserZoomLevelIncrement = value;
                OnPropertyChanged();
            }
        }
    }

    public FadeSpeed FadeSpeedType
    {
        get => _optionsService.ImageFadeSpeed;
        set
        {
            if (_optionsService.ImageFadeSpeed != value)
            {
                _optionsService.ImageFadeSpeed = value;
                OnPropertyChanged();
            }
        }
    }

    public IEnumerable<LoggingLevel> LoggingLevels => _loggingLevels;

    public LogEventLevel LogEventLevel
    {
        get => _optionsService.LogEventLevel;
        set
        {
            if (_optionsService.LogEventLevel != value)
            {
                _optionsService.LogEventLevel = value;
                OnPropertyChanged();
            }
        }
    }

    public IEnumerable<RenderingMethodItem> RenderingMethods => _renderingMethods;

    public RenderingMethod RenderingMethod
    {
        get => _optionsService.RenderingMethod;
        set
        {
            if (_optionsService.RenderingMethod != value)
            {
                _optionsService.RenderingMethod = value;
                OnPropertyChanged();
            }
        }
    }

    public IEnumerable<LanguageItem> Languages => _languages;

    public string? LanguageId
    {
        get => _optionsService.Culture;
        set
        {
            if (_optionsService.Culture != value)
            {
                _optionsService.Culture = value;
                OnPropertyChanged();
            }
        }
    }

    public IEnumerable<MonitorItem> Monitors => _monitors;

    public bool CanChangeMonitor => !MediaWindowed;

    public string? MonitorId
    {
        get => _optionsService.MediaMonitorId;
        set
        {
            if (_optionsService.MediaMonitorId != value)
            {
                if (value == null && IsMediaActive)
                {
                    // prevent selection of "none" when media is active.
                    _snackbarService.EnqueueWithOk(Properties.Resources.NO_DESELECT_MONITOR, Properties.Resources.OK);
                }
                else
                {
                    _optionsService.MediaMonitorId = value;
                    OnPropertyChanged();
                }
            }
        }
    }

    public bool WindowedAlwaysOnTop
    {
        get => _optionsService.WindowedAlwaysOnTop;
        set
        {
            if (_optionsService.WindowedAlwaysOnTop != value)
            {
                _optionsService.WindowedAlwaysOnTop = value;
                OnPropertyChanged();
            }
        }
    }

    public bool MediaWindowed
    {
        get => _optionsService.MediaWindowed;
        set
        {
            if (_optionsService.MediaWindowed != value)
            {
                if (!value && IsMediaActive && _optionsService.MediaMonitorId == null)
                {
                    // prevent unchecking of windowed mode when media is active.
                    _snackbarService.EnqueueWithOk(Properties.Resources.NO_DESELECT_WINDOWED, Properties.Resources.OK);
                }
                else
                {
                    _optionsService.MediaWindowed = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanChangeMonitor));
                }
            }
        }
    }

    public bool Is360PSize => MediaWindowSize == Size360P;

    public bool Is480PSize => MediaWindowSize == Size480P;

    public bool Is720PSize => MediaWindowSize == Size720P;

    public bool Is1080PSize => MediaWindowSize == Size1080P;

    public bool MediaWindowResizable
    {
        get => MediaWindowSize.IsEmpty;
        set
        {
            if (value)
            {
                MediaWindowSize = Size.Empty;
                OnPropertyChanged();
            }
        }
    }

    public bool MediaWindowFixed
    {
        get => !MediaWindowSize.IsEmpty;
        set
        {
            if (value)
            {
                MediaWindowSize = Size720P;
                OnPropertyChanged();
            }
        }
    }

    public int? MediaWindowWidth
    {
        get => MediaWindowSize.IsEmpty ? null : (int)MediaWindowSize.Width;
        set
        {
            if ((value ?? 0) < 0)
            {
                return;
            }

            MediaWindowSize = value.HasValue ? new Size(value.Value, MediaWindowSize.Height) : Size.Empty;
        }
    }

    public int? MediaWindowHeight
    {
        get => MediaWindowSize.IsEmpty ? null : (int)MediaWindowSize.Height;
        set
        {
            if ((value ?? 0) < 0)
            {
                return;
            }

            MediaWindowSize = value.HasValue ? new Size(MediaWindowSize.Width, value.Value) : Size.Empty;
        }
    }

    public Size MediaWindowSize
    {
        get => _optionsService.MediaWindowSize;
        set
        {
            _optionsService.MediaWindowSize = NormalizeSize(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(MediaWindowResizable));
            OnPropertyChanged(nameof(MediaWindowFixed));

            OnPropertyChanged(nameof(MediaWindowWidth));
            OnPropertyChanged(nameof(MediaWindowHeight));

            OnPropertyChanged(nameof(Is360PSize));
            OnPropertyChanged(nameof(Is480PSize));
            OnPropertyChanged(nameof(Is720PSize));
            OnPropertyChanged(nameof(Is1080PSize));
        }
    }

    private static Size NormalizeSize(Size value)
    {
        if (value.IsEmpty)
        {
            return value;
        }

        return new Size
        {
            Width = LimitNumber(MinSize.Width, value.Width, MaxSize.Width),
            Height = LimitNumber(MinSize.Height, value.Height, MaxSize.Height)
        };
    }

    private static double LimitNumber(double lowerLimitIncl, double value, double upperLimitIncl)
    {
        if (value < lowerLimitIncl)
        {
            return lowerLimitIncl;
        }

        if (value > upperLimitIncl)
        {
            return upperLimitIncl;
        }

        return value;
    }

    private void OnShutDown(object? sender, ShutDownMessage obj)
    {
        _optionsService.RecentlyUsedMediaFolders = _recentlyUsedMediaFolders.GetFolders().ToList();
        _optionsService.Save();
    }

    private static MagnifierShapeItem[] GetMagnifierShapes()
    {
        var values = Enum.GetValues<MagnifierShape>();
        var result = new List<MagnifierShapeItem>(values.Length);

        foreach (var v in values)
        {
            result.Add(new MagnifierShapeItem
            {
                Shape = v,
                Name = v.GetDescriptiveName(),
            });
        }

        return result.ToArray();
    }

    private static MagnifierSizeItem[] GetMagnifierSizes()
    {
        var result = new List<MagnifierSizeItem>();

        foreach (var v in Enum.GetValues<MagnifierSize>())
        {
            result.Add(new MagnifierSizeItem
            {
                Size = v,
                Name = v.GetDescriptiveName(),
            });
        }

        return result.ToArray();
    }

    private static ImageFadeSpeed[] GetFadingSpeedTypes()
    {
        var result = new List<ImageFadeSpeed>();

        foreach (var v in Enum.GetValues<FadeSpeed>())
        {
            result.Add(new ImageFadeSpeed
            {
                Speed = v,
                Name = v.GetDescriptiveName(),
            });
        }

        return result.ToArray();
    }

    private static ImageFade[] GetImageFadingTypes()
    {
        var result = new List<ImageFade>();

        foreach (var v in Enum.GetValues<ImageFadeType>())
        {
            result.Add(new ImageFade
            {
                Fade = v,
                Name = v.GetDescriptiveName(),
            });
        }

        return result.ToArray();
    }

    private static LoggingLevel[] GetLoggingLevels()
    {
        var result = new List<LoggingLevel>();

        foreach (var v in Enum.GetValues<LogEventLevel>())
        {
            result.Add(new LoggingLevel
            {
                Level = v,
                Name = v.GetDescriptiveName(),
            });
        }

        return result.ToArray();
    }

    private static RenderingMethodItem[] GetRenderingMethods() =>
        // don't localize these strings!
        [
        new() { Method = RenderingMethod.MediaFoundation, Name = "Media Foundation" },
        new() { Method = RenderingMethod.Ffmpeg, Name = "Ffmpeg" }
        ];

    private MonitorItem[] GetSystemMonitors()
    {
        var result = new List<MonitorItem>
        {
            // empty (i.e. no timer monitor)
            new()
            {
                MonitorName = Properties.Resources.MONITOR_NONE,
                FriendlyName = Properties.Resources.MONITOR_NONE,
            },
        };

        var monitors = _monitorsService.GetSystemMonitors();
        result.AddRange(monitors.Select(x => new MonitorItem(x)));

        return result.ToArray();
    }

    private void InitRecentlyUsedFolders()
    {
        for (var n = _optionsService.RecentlyUsedMediaFolders.Count - 1; n >= 0; --n)
        {
            _recentlyUsedMediaFolders.Add(_optionsService.RecentlyUsedMediaFolders[n]);
        }
    }

    private void HandleNavigationEvent(object? sender, NavigationEventArgs e)
    {
        if (e.PageName != null && e.PageName.Equals(_pageService.SettingsPageName, StringComparison.Ordinal))
        {
            // when Settings page is shown.
            IsMediaActive = _activeMediaItemsService.Any();
        }
    }

    private void InitCommands()
    {
        PurgeThumbnailCacheCommand = new RelayCommand(PurgeThumbnailCache);
        PurgeWebCacheCommand = new RelayCommand(PurgeWebCache);
        OpenMediaFolderCommand = new RelayCommand(OpenMediaFolder);
        Set360PSizeCommand = new RelayCommand(() => SetMediaWindowSize(Size360P));
        Set480PSizeCommand = new RelayCommand(() => SetMediaWindowSize(Size480P));
        Set720PSizeCommand = new RelayCommand(() => SetMediaWindowSize(Size720P));
        Set1080PSizeCommand = new RelayCommand(() => SetMediaWindowSize(Size1080P));
    }

    private void PurgeWebCache()
    {
        _optionsService.ShouldPurgeBrowserCacheOnStartup = true;
        OnPropertyChanged(nameof(IsBrowserCachePurgeQueued));
    }

    private void OpenMediaFolder()
    {
        var dialog = new CommonOpenFileDialog
        {
            IsFolderPicker = true,
            Title = Properties.Resources.MEDIA_FOLDER_BROWSE,
            InitialDirectory = GetMediaFolderBrowsingStart(),
            AddToMostRecentlyUsedList = false,
            DefaultDirectory = GetMediaFolderBrowsingStart(),
            EnsureFileExists = true,
            EnsurePathExists = true,
            EnsureReadOnly = false,
            EnsureValidNames = true,
            Multiselect = false,
            ShowPlacesList = true,
        };

        var result = dialog.ShowDialog();
        if (result == CommonFileDialogResult.Ok)
        {
            _recentlyUsedMediaFolders.Add(dialog.FileName);
            MediaFolder = dialog.FileName;
            OnPropertyChanged(nameof(RecentMediaFolders));
        }
    }

    private string? GetMediaFolderBrowsingStart()
    {
        if (!string.IsNullOrEmpty(MediaFolder) && Directory.Exists(MediaFolder))
        {
            return MediaFolder;
        }

        return null;
    }

    private void PurgeThumbnailCache() => _thumbnailService.ClearThumbCache();

    private void SetMediaWindowSize(Size size) => MediaWindowSize = size;

    private static LanguageItem[] GetSupportedLanguages()
    {
        var result = new List<LanguageItem>();

        var subFolders = Directory.GetDirectories(AppContext.BaseDirectory);

        foreach (var folder in subFolders)
        {
            if (!string.IsNullOrEmpty(folder))
            {
                try
                {
                    var c = new CultureInfo(Path.GetFileNameWithoutExtension(folder));
                    result.Add(new LanguageItem
                    {
                        LanguageId = c.Name,
                        LanguageName = c.EnglishName,
                    });
                }
                catch (CultureNotFoundException)
                {
                    // expected
                }
            }
        }

        // the native language
        var cNative = new CultureInfo(Path.GetFileNameWithoutExtension("en-GB"));
        result.Add(new LanguageItem
        {
            LanguageId = cNative.Name,
            LanguageName = cNative.EnglishName,
        });

        result.Sort((x, y) => string.CompareOrdinal(x.LanguageName, y.LanguageName));

        return result.ToArray();
    }

    private static MirrorHotKeyItem[] GetMirrorHotKeys()
    {
        var result = new List<MirrorHotKeyItem>();

        for (var ch = 'A'; ch <= 'Z'; ++ch)
        {
            result.Add(new MirrorHotKeyItem { Character = ch, Name = $"ALT+{ch}" });
        }

        return result.ToArray();
    }
}
