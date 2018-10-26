namespace OnlyM.ViewModel
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
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
    using PubSubMessages;
    using Serilog.Events;
    using Services;
    using Services.MediaChanging;
    using Services.Pages;
    using Services.Snackbar;

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
        private readonly RenderingMethodItem[] _renderingMethods;
        private readonly LoggingLevel[] _loggingLevels;
        private readonly ImageFade[] _fadingTypes;
        private readonly ImageFadeSpeed[] _fadingSpeeds;
        private readonly RecentlyUsedFolders _recentlyUsedMediaFolders;

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
            
            _monitors = GetSystemMonitors().ToArray();
            _loggingLevels = GetLoggingLevels().ToArray();
            _fadingTypes = GetImageFadingTypes().ToArray();
            _fadingSpeeds = GetFadingSpeedTypes().ToArray();
            _renderingMethods = GetRenderingMethods().ToArray();
            
            _pageService.NavigationEvent += HandleNavigationEvent;
            
            InitCommands();
            Messenger.Default.Register<ShutDownMessage>(this, OnShutDown);
        }

        public RelayCommand PurgeThumbnailCacheCommand { get; set; }

        public RelayCommand OpenMediaFolderCommand { get; set; }

        public ObservableCollection<string> RecentMediaFolders => _recentlyUsedMediaFolders.GetFolders();

        public string AppVersionStr => string.Format(Properties.Resources.APP_VER, VersionDetection.GetCurrentVersion());

        public IEnumerable<ImageFadeSpeed> FadeSpeedTypes => _fadingSpeeds;

        public IEnumerable<ImageFade> ImageFadeTypes => _fadingTypes;

        public string MaxItemCount
        {
            get => _optionsService.Options.MaxItemCount.ToString();
            set
            {
                if (!string.IsNullOrEmpty(value) && !_optionsService.Options.MaxItemCount.ToString().Equals(value))
                {
                    if (int.TryParse(value, out var count))
                    {
                        _optionsService.Options.MaxItemCount = count;
                        RaisePropertyChanged();
                    }
                }
            }
        }

        public DateTime MediaCalendarDate
        {
            get => _optionsService.Options.OperatingDate;
            set
            {
                if (_optionsService.Options.OperatingDate != value)
                {
                    _optionsService.Options.OperatingDate = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool AlwaysOnTop
        {
            get => _optionsService.Options.AlwaysOnTop;
            set
            {
                if (_optionsService.Options.AlwaysOnTop != value)
                {
                    _optionsService.Options.AlwaysOnTop = value;
                    RaisePropertyChanged();
                }
            }
        }

        public int VideoScreenLeftMargin
        {
            get => _optionsService.Options.VideoScreenPosition.LeftMarginPercentage;
            set
            {
                if (_optionsService.Options.VideoScreenPosition.LeftMarginPercentage != value)
                {
                    var newPos = (ScreenPosition)_optionsService.Options.VideoScreenPosition.Clone();
                    ScreenPositionHelper.ModifyScreenPosition(newPos, ScreenMarginSide.Left, value, out var opposingMarginChanged);

                    _optionsService.Options.VideoScreenPosition = newPos;
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
            get => _optionsService.Options.VideoScreenPosition.RightMarginPercentage;
            set
            {
                if (_optionsService.Options.VideoScreenPosition.RightMarginPercentage != value)
                {
                    var newPos = (ScreenPosition)_optionsService.Options.VideoScreenPosition.Clone();
                    ScreenPositionHelper.ModifyScreenPosition(newPos, ScreenMarginSide.Right, value, out var opposingMarginChanged);

                    _optionsService.Options.VideoScreenPosition = newPos;
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
            get => _optionsService.Options.VideoScreenPosition.TopMarginPercentage;
            set
            {
                if (_optionsService.Options.VideoScreenPosition.TopMarginPercentage != value)
                {
                    var newPos = (ScreenPosition)_optionsService.Options.VideoScreenPosition.Clone();
                    ScreenPositionHelper.ModifyScreenPosition(newPos, ScreenMarginSide.Top, value, out var opposingMarginChanged);

                    _optionsService.Options.VideoScreenPosition = newPos;
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
            get => _optionsService.Options.VideoScreenPosition.BottomMarginPercentage;
            set
            {
                if (_optionsService.Options.VideoScreenPosition.BottomMarginPercentage != value)
                {
                    var newPos = (ScreenPosition)_optionsService.Options.VideoScreenPosition.Clone();
                    ScreenPositionHelper.ModifyScreenPosition(newPos, ScreenMarginSide.Bottom, value, out var opposingMarginChanged);

                    _optionsService.Options.VideoScreenPosition = newPos;
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
            get => _optionsService.Options.ImageScreenPosition.LeftMarginPercentage;
            set
            {
                if (_optionsService.Options.ImageScreenPosition.LeftMarginPercentage != value)
                {
                    var newPos = (ScreenPosition)_optionsService.Options.ImageScreenPosition.Clone();
                    ScreenPositionHelper.ModifyScreenPosition(newPos, ScreenMarginSide.Left, value, out var opposingMarginChanged);

                    _optionsService.Options.ImageScreenPosition = newPos;
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
            get => _optionsService.Options.ImageScreenPosition.RightMarginPercentage;
            set
            {
                if (_optionsService.Options.ImageScreenPosition.RightMarginPercentage != value)
                {
                    var newPos = (ScreenPosition)_optionsService.Options.ImageScreenPosition.Clone();
                    ScreenPositionHelper.ModifyScreenPosition(newPos, ScreenMarginSide.Right, value, out var opposingMarginChanged);

                    _optionsService.Options.ImageScreenPosition = newPos;
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
            get => _optionsService.Options.ImageScreenPosition.TopMarginPercentage;
            set
            {
                if (_optionsService.Options.ImageScreenPosition.TopMarginPercentage != value)
                {
                    var newPos = (ScreenPosition)_optionsService.Options.ImageScreenPosition.Clone();
                    ScreenPositionHelper.ModifyScreenPosition(newPos, ScreenMarginSide.Top, value, out var opposingMarginChanged);

                    _optionsService.Options.ImageScreenPosition = newPos;
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
            get => _optionsService.Options.ImageScreenPosition.BottomMarginPercentage;
            set
            {
                if (_optionsService.Options.ImageScreenPosition.BottomMarginPercentage != value)
                {
                    var newPos = (ScreenPosition)_optionsService.Options.ImageScreenPosition.Clone();
                    ScreenPositionHelper.ModifyScreenPosition(newPos, ScreenMarginSide.Bottom, value, out var opposingMarginChanged);

                    _optionsService.Options.ImageScreenPosition = newPos;
                    RaisePropertyChanged();

                    if (opposingMarginChanged)
                    {
                        RaisePropertyChanged(nameof(ImageScreenTopMargin));
                    }
                }
            }
        }

        public bool ShowCommandPanel
        {
            get => _optionsService.Options.ShowMediaItemCommandPanel;
            set
            {
                if (_optionsService.Options.ShowMediaItemCommandPanel != value)
                {
                    _optionsService.Options.ShowMediaItemCommandPanel = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool UseInternalMediaTitles
        {
            get => _optionsService.Options.UseInternalMediaTitles;
            set
            {
                if (_optionsService.Options.UseInternalMediaTitles != value)
                {
                    _optionsService.Options.UseInternalMediaTitles = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool IncludeBlankScreenItem
        {
            get => _optionsService.Options.IncludeBlanksScreenItem;
            set
            {
                if (_optionsService.Options.IncludeBlanksScreenItem != value)
                {
                    _optionsService.Options.IncludeBlanksScreenItem = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool JwLibModeNotSet => !JwLibraryCompatibilityMode;

        public bool JwLibraryCompatibilityMode
        {
            get => _optionsService.Options.JwLibraryCompatibilityMode;
            set
            {
                if (_optionsService.Options.JwLibraryCompatibilityMode != value)
                {
                    _optionsService.Options.JwLibraryCompatibilityMode = value;
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
            get => _optionsService.Options.PermanentBackdrop;
            set
            {
                if (_optionsService.Options.PermanentBackdrop != value)
                {
                    _optionsService.Options.PermanentBackdrop = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(NotPermanentBackdrop));
                }
            }
        }

        public bool NotPermanentBackdrop => !PermanentBackdrop;
        
        public bool ShowVideoSubtitles
        {
            get => _optionsService.Options.ShowVideoSubtitles;
            set
            {
                if (_optionsService.Options.ShowVideoSubtitles != value)
                {
                    _optionsService.Options.ShowVideoSubtitles = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool AllowVideoScrubbing
        {
            get => _optionsService.Options.AllowVideoScrubbing;
            set
            {
                if (_optionsService.Options.AllowVideoScrubbing != value)
                {
                    _optionsService.Options.AllowVideoScrubbing = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool AllowVideoPositionSeeking
        {
            get => _optionsService.Options.AllowVideoPositionSeeking;
            set
            {
                if (_optionsService.Options.AllowVideoPositionSeeking != value)
                {
                    _optionsService.Options.AllowVideoPositionSeeking = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool ConfirmWhenStoppingVideo
        {
            get => _optionsService.Options.ConfirmVideoStop;
            set
            {
                if (_optionsService.Options.ConfirmVideoStop != value)
                {
                    _optionsService.Options.ConfirmVideoStop = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool AllowVideoPause
        {
            get => _optionsService.Options.AllowVideoPause;
            set
            {
                if (_optionsService.Options.AllowVideoPause != value)
                {
                    _optionsService.Options.AllowVideoPause = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool ShowFreezeCommand
        {
            get => _optionsService.Options.ShowFreezeCommand;
            set
            {
                if (_optionsService.Options.ShowFreezeCommand != value)
                {
                    _optionsService.Options.ShowFreezeCommand = value;
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

        public bool IsMediaFolderOverriden => _optionsService.Options.IsCommandLineMediaFolderSpecified();

        public bool IsMediaInactive => !IsMediaActive;

        public bool EmbeddedThumbnails
        {
            get => _optionsService.Options.EmbeddedThumbnails;
            set
            {
                if (_optionsService.Options.EmbeddedThumbnails != value)
                {
                    _optionsService.Options.EmbeddedThumbnails = value;
                    RaisePropertyChanged();
                    PurgeThumbnailCache();
                }
            }
        }

        public bool CacheImages
        {
            get => _optionsService.Options.CacheImages;
            set
            {
                if (_optionsService.Options.CacheImages != value)
                {
                    _optionsService.Options.CacheImages = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool AutoRotateImages
        {
            get => _optionsService.Options.AutoRotateImages;
            set
            {
                if (_optionsService.Options.AutoRotateImages != value)
                {
                    _optionsService.Options.AutoRotateImages = value;
                    RaisePropertyChanged();
                }
            }
        }

        public string MediaFolder
        {
            get => _optionsService.Options.MediaFolder;
            set
            {
                if (_optionsService.Options.MediaFolder != value)
                {
                    _optionsService.Options.MediaFolder = value;
                    RaisePropertyChanged();
                }
            }
        }

        public ImageFadeType ImageFadeType
        {
            get => _optionsService.Options.ImageFadeType;
            set
            {
                if (_optionsService.Options.ImageFadeType != value)
                {
                    _optionsService.Options.ImageFadeType = value;
                    RaisePropertyChanged();
                }
            }
        }

        public FadeSpeed FadeSpeedType
        {
            get => _optionsService.Options.ImageFadeSpeed;
            set
            {
                if (_optionsService.Options.ImageFadeSpeed != value)
                {
                    _optionsService.Options.ImageFadeSpeed = value;
                    RaisePropertyChanged();
                }
            }
        }

        public IEnumerable<LoggingLevel> LoggingLevels => _loggingLevels;

        public LogEventLevel LogEventLevel
        {
            get => _optionsService.Options.LogEventLevel;
            set
            {
                if (_optionsService.Options.LogEventLevel != value)
                {
                    _optionsService.Options.LogEventLevel = value;
                    RaisePropertyChanged();
                }
            }
        }

        public IEnumerable<RenderingMethodItem> RenderingMethods => _renderingMethods;

        public RenderingMethod RenderingMethod
        {
            get => _optionsService.Options.RenderingMethod;
            set
            {
                if (_optionsService.Options.RenderingMethod != value)
                {
                    _optionsService.Options.RenderingMethod = value;
                    RaisePropertyChanged();
                }
            }
        }

        public IEnumerable<MonitorItem> Monitors => _monitors;

        public string MonitorId
        {
            get => _optionsService.Options.MediaMonitorId;
            set
            {
                if (_optionsService.Options.MediaMonitorId != value)
                {
                    if (value == null && IsMediaActive)
                    {
                        // prevent selection of "none" when media is active.
                        _snackbarService.EnqueueWithOk(Properties.Resources.NO_DESELECT_MONITOR);
                    }
                    else
                    {
                        _optionsService.Options.MediaMonitorId = value;
                        RaisePropertyChanged();
                    }
                }
            }
        }

        private void OnShutDown(ShutDownMessage obj)
        {
            _optionsService.Options.RecentlyUsedMediaFolders = _recentlyUsedMediaFolders.GetFolders().ToList();
            _optionsService.Save();
        }
        
        private IEnumerable<ImageFadeSpeed> GetFadingSpeedTypes()
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

            return result;
        }
        
        private IEnumerable<ImageFade> GetImageFadingTypes()
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

            return result;
        }

        private IEnumerable<LoggingLevel> GetLoggingLevels()
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

            return result;
        }

        private IEnumerable<RenderingMethodItem> GetRenderingMethods()
        {
            // don't localize these strings!
            return new List<RenderingMethodItem>
            {
                new RenderingMethodItem { Method = RenderingMethod.MediaFoundation, Name = @"Media Foundation" },
                new RenderingMethodItem { Method = RenderingMethod.Ffmpeg, Name = @"Ffmpeg" }
            };
        }

        private IEnumerable<MonitorItem> GetSystemMonitors()
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

            return result;
        }

        private void InitRecentlyUsedFolders()
        {
            for (int n = _optionsService.Options.RecentlyUsedMediaFolders.Count - 1; n >= 0; --n)
            {
                _recentlyUsedMediaFolders.Add(_optionsService.Options.RecentlyUsedMediaFolders[n]);
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
            OpenMediaFolderCommand = new RelayCommand(OpenMediaFolder);
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
    }
}
