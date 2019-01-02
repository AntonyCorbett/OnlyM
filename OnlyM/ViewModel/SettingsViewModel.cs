namespace OnlyM.ViewModel
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using AutoUpdates;
    using Core.Extensions;
    using Core.Models;
    using Core.Services.Media;
    using Core.Services.Monitors;
    using Core.Services.Options;
    using GalaSoft.MvvmLight;
    using GalaSoft.MvvmLight.CommandWpf;
    using GalaSoft.MvvmLight.Messaging;
    using Microsoft.WindowsAPICodePack.Dialogs;
    using Models;
    using OnlyM.CoreSys.Services.Snackbar;
    using PubSubMessages;
    using Serilog.Events;
    using Services;
    using Services.MediaChanging;
    using Services.Pages;
    
    // ReSharper disable once UnusedMember.Global
    internal class SettingsViewModel : ViewModelBase
    {
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
            Messenger.Default.Register<ShutDownMessage>(this, OnShutDown);
        }
        
        public RelayCommand PurgeThumbnailCacheCommand { get; set; }

        public RelayCommand PurgeWebCacheCommand { get; set; }

        public RelayCommand OpenMediaFolderCommand { get; set; }

        public ObservableCollection<string> RecentMediaFolders => _recentlyUsedMediaFolders.GetFolders();

        public string AppVersionStr => string.Format(Properties.Resources.APP_VER, VersionDetection.GetCurrentVersion());

        public IEnumerable<ImageFadeSpeed> FadeSpeedTypes => _fadingSpeeds;

        public IEnumerable<ImageFade> ImageFadeTypes => _fadingTypes;

        public IEnumerable<MagnifierShapeItem> MagnifierShapes => _magnifierShapes;

        public IEnumerable<MagnifierSizeItem> MagnifierSizes => _magnifierSizes;

        public IEnumerable<MirrorHotKeyItem> MirrorHotKeys => _mirrorHotKeys;

        public bool IsBrowserCachePurgeQueued => _optionsService.ShouldPurgeBrowserCacheOnStartup;
        
        public string MaxItemCount
        {
            get => _optionsService.MaxItemCount.ToString();
            set
            {
                if (!string.IsNullOrEmpty(value) && !_optionsService.MaxItemCount.ToString().Equals(value))
                {
                    if (int.TryParse(value, out var count))
                    {
                        _optionsService.MaxItemCount = count;
                        RaisePropertyChanged();
                    }
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
                    RaisePropertyChanged();
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
                    RaisePropertyChanged();
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
                    RaisePropertyChanged();

                    if (opposingMarginChanged)
                    {
                        RaisePropertyChanged(nameof(VideoScreenRightMargin));
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
                    RaisePropertyChanged();

                    if (opposingMarginChanged)
                    {
                        RaisePropertyChanged(nameof(VideoScreenLeftMargin));
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
                    RaisePropertyChanged();

                    if (opposingMarginChanged)
                    {
                        RaisePropertyChanged(nameof(VideoScreenBottomMargin));
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
                    RaisePropertyChanged();

                    if (opposingMarginChanged)
                    {
                        RaisePropertyChanged(nameof(VideoScreenTopMargin));
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
                    RaisePropertyChanged();

                    if (opposingMarginChanged)
                    {
                        RaisePropertyChanged(nameof(ImageScreenRightMargin));
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
                    RaisePropertyChanged();

                    if (opposingMarginChanged)
                    {
                        RaisePropertyChanged(nameof(ImageScreenLeftMargin));
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
                    RaisePropertyChanged();

                    if (opposingMarginChanged)
                    {
                        RaisePropertyChanged(nameof(ImageScreenBottomMargin));
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
                    RaisePropertyChanged();

                    if (opposingMarginChanged)
                    {
                        RaisePropertyChanged(nameof(ImageScreenTopMargin));
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
                    RaisePropertyChanged();

                    if (opposingMarginChanged)
                    {
                        RaisePropertyChanged(nameof(WebScreenRightMargin));
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
                    RaisePropertyChanged();

                    if (opposingMarginChanged)
                    {
                        RaisePropertyChanged(nameof(WebScreenLeftMargin));
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
                    RaisePropertyChanged();

                    if (opposingMarginChanged)
                    {
                        RaisePropertyChanged(nameof(WebScreenBottomMargin));
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
                    RaisePropertyChanged();

                    if (opposingMarginChanged)
                    {
                        RaisePropertyChanged(nameof(WebScreenTopMargin));
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
                    RaisePropertyChanged();
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
                    RaisePropertyChanged();
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
                    RaisePropertyChanged();
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
                    RaisePropertyChanged();
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
                    RaisePropertyChanged();
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
                    RaisePropertyChanged();
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
                    RaisePropertyChanged();
                }
            }
        }

        public bool JwLibModeNotSet => !JwLibraryCompatibilityMode;

        public bool JwLibraryCompatibilityMode
        {
            get => _optionsService.JwLibraryCompatibilityMode;
            set
            {
                if (_optionsService.JwLibraryCompatibilityMode != value)
                {
                    _optionsService.JwLibraryCompatibilityMode = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(JwLibModeNotSet));

                    if (value)
                    {
                        PermanentBackdrop = false;
                    }
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
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(NotPermanentBackdrop));
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
                    RaisePropertyChanged();
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
                    RaisePropertyChanged();
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
                    RaisePropertyChanged();
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
                    RaisePropertyChanged();
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
                    RaisePropertyChanged();
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
                    RaisePropertyChanged();
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
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(IsMediaInactive));
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
                    RaisePropertyChanged();
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
                    RaisePropertyChanged();
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
                    RaisePropertyChanged();
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
                    RaisePropertyChanged();
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
                    RaisePropertyChanged();
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
                    RaisePropertyChanged();
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
                    RaisePropertyChanged();
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
                    RaisePropertyChanged();
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
                    RaisePropertyChanged();
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
                    RaisePropertyChanged();
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
                    RaisePropertyChanged();
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
                    RaisePropertyChanged();
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
                    RaisePropertyChanged();
                }
            }
        }

        public IEnumerable<LanguageItem> Languages => _languages;

        public string LanguageId
        {
            get => _optionsService.Culture;
            set
            {
                if (_optionsService.Culture != value)
                {
                    _optionsService.Culture = value;
                    RaisePropertyChanged();
                }
            }
        }

        public IEnumerable<MonitorItem> Monitors => _monitors;

        public string MonitorId
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
                        RaisePropertyChanged();
                    }
                }
            }
        }

        private void OnShutDown(ShutDownMessage obj)
        {
            _optionsService.RecentlyUsedMediaFolders = _recentlyUsedMediaFolders.GetFolders().ToList();
            _optionsService.Save();
        }

        private MagnifierShapeItem[] GetMagnifierShapes()
        {
            var result = new List<MagnifierShapeItem>();

            foreach (MagnifierShape v in Enum.GetValues(typeof(MagnifierShape)))
            {
                result.Add(new MagnifierShapeItem
                {
                    Shape = v,
                    Name = v.GetDescriptiveName()
                });
            }

            return result.ToArray();
        }

        private MagnifierSizeItem[] GetMagnifierSizes()
        {
            var result = new List<MagnifierSizeItem>();

            foreach (MagnifierSize v in Enum.GetValues(typeof(MagnifierSize)))
            {
                result.Add(new MagnifierSizeItem
                {
                    Size = v,
                    Name = v.GetDescriptiveName()
                });
            }

            return result.ToArray();
        }

        private ImageFadeSpeed[] GetFadingSpeedTypes()
        {
            var result = new List<ImageFadeSpeed>();

            foreach (FadeSpeed v in Enum.GetValues(typeof(FadeSpeed)))
            {
                result.Add(new ImageFadeSpeed
                {
                    Speed = v,
                    Name = v.GetDescriptiveName()
                });
            }

            return result.ToArray();
        }
        
        private ImageFade[] GetImageFadingTypes()
        {
            var result = new List<ImageFade>();

            foreach (ImageFadeType v in Enum.GetValues(typeof(ImageFadeType)))
            {
                result.Add(new ImageFade
                {
                    Fade = v,
                    Name = v.GetDescriptiveName()
                });
            }

            return result.ToArray();
        }

        private LoggingLevel[] GetLoggingLevels()
        {
            var result = new List<LoggingLevel>();

            foreach (LogEventLevel v in Enum.GetValues(typeof(LogEventLevel)))
            {
                result.Add(new LoggingLevel
                {
                    Level = v,
                    Name = v.GetDescriptiveName()
                });
            }

            return result.ToArray();
        }

        private RenderingMethodItem[] GetRenderingMethods()
        {
            // don't localize these strings!
            return new[]
            {
                new RenderingMethodItem { Method = RenderingMethod.MediaFoundation, Name = @"Media Foundation" },
                new RenderingMethodItem { Method = RenderingMethod.Ffmpeg, Name = @"Ffmpeg" }
            };
        }

        private MonitorItem[] GetSystemMonitors()
        {
            var result = new List<MonitorItem>
            {
                // empty (i.e. no timer monitor)
                new MonitorItem
                {
                    MonitorName = Properties.Resources.MONITOR_NONE,
                    FriendlyName = Properties.Resources.MONITOR_NONE
                }
            };

            var monitors = _monitorsService.GetSystemMonitors();
            result.AddRange(monitors.Select(AutoMapper.Mapper.Map<MonitorItem>));

            return result.ToArray();
        }

        private void InitRecentlyUsedFolders()
        {
            for (int n = _optionsService.RecentlyUsedMediaFolders.Count - 1; n >= 0; --n)
            {
                _recentlyUsedMediaFolders.Add(_optionsService.RecentlyUsedMediaFolders[n]);
            }
        }

        private void HandleNavigationEvent(object sender, NavigationEventArgs e)
        {
            if (e.PageName.Equals(_pageService.SettingsPageName))
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
        }

        private void PurgeWebCache()
        {
            _optionsService.ShouldPurgeBrowserCacheOnStartup = true;
            RaisePropertyChanged(nameof(IsBrowserCachePurgeQueued));
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
                ShowPlacesList = true
            };

            var result = dialog.ShowDialog();
            if (result == CommonFileDialogResult.Ok)
            {
                _recentlyUsedMediaFolders.Add(dialog.FileName);
                MediaFolder = dialog.FileName;
                RaisePropertyChanged(nameof(RecentMediaFolders));
            }
        }

        private string GetMediaFolderBrowsingStart()
        {
            if (!string.IsNullOrEmpty(MediaFolder) && Directory.Exists(MediaFolder))
            {
                return MediaFolder;
            }

            return null;
        }

        private void PurgeThumbnailCache()
        {
            _thumbnailService.ClearThumbCache();
        }

        private LanguageItem[] GetSupportedLanguages()
        {
            var result = new List<LanguageItem>();

            var subFolders = Directory.GetDirectories(AppDomain.CurrentDomain.BaseDirectory);

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
                            LanguageName = c.EnglishName
                        });
                    }
                    catch (CultureNotFoundException)
                    {
                        // expected
                    }
                }
            }

            // the native language
            {
                var c = new CultureInfo(Path.GetFileNameWithoutExtension("en-GB"));
                result.Add(new LanguageItem
                {
                    LanguageId = c.Name,
                    LanguageName = c.EnglishName
                });
            }

            result.Sort((x, y) => string.Compare(x.LanguageName, y.LanguageName, StringComparison.Ordinal));

            return result.ToArray();
        }

        private MirrorHotKeyItem[] GetMirrorHotKeys()
        {
            var result = new List<MirrorHotKeyItem>();

            for (var ch = 'A'; ch <= 'Z'; ++ch)
            {
                result.Add(new MirrorHotKeyItem { Character = ch, Name = $"ALT+{ch}" });
            }

            return result.ToArray();
        }
    }
}
