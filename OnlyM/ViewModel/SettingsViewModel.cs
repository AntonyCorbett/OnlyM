namespace OnlyM.ViewModel
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AutoUpdates;
    using Core.Extensions;
    using Core.Models;
    using Core.Services.Media;
    using Core.Services.Monitors;
    using Core.Services.Options;
    using GalaSoft.MvvmLight;
    using GalaSoft.MvvmLight.Command;
    using GalaSoft.MvvmLight.Messaging;
    using Models;
    using PubSubMessages;
    using Serilog.Events;
    using Services.Pages;

    // ReSharper disable once ClassNeverInstantiated.Global
    internal class SettingsViewModel : ViewModelBase
    {
        private readonly IPageService _pageService;
        private readonly IMonitorsService _monitorsService;
        private readonly IOptionsService _optionsService;
        private readonly IThumbnailService _thumbnailService;
        private readonly MonitorItem[] _monitors;
        private readonly LoggingLevel[] _loggingLevels;
        private readonly ImageFade[] _fadingTypes;
        private readonly ImageFadeSpeed[] _fadingSpeeds;

        public SettingsViewModel(
            IPageService pageService, 
            IMonitorsService monitorsService,
            IOptionsService optionsService,
            IThumbnailService thumbnailService)
        {
            _pageService = pageService;
            _monitorsService = monitorsService;
            _optionsService = optionsService;
            _thumbnailService = thumbnailService;

            _monitors = GetSystemMonitors().ToArray();
            _loggingLevels = GetLoggingLevels().ToArray();
            _fadingTypes = GetImageFadingTypes().ToArray();
            _fadingSpeeds = GetFadingSpeedTypes().ToArray();

            _pageService.NavigationEvent += HandleNavigationEvent;

            InitCommands();
            Messenger.Default.Register<ShutDownMessage>(this, OnShutDown);
        }

        private void HandleNavigationEvent(object sender, NavigationEventArgs e)
        {
            if (e.PageName.Equals(_pageService.SettingsPageName))
            {
                // when Settings page is shown.
                IsMediaActive = _pageService.IsMediaItemActive;
            }
        }

        private void InitCommands()
        {
            PurgeThumbnailCacheCommand = new RelayCommand(PurgeThumbnailCache);
        }

        private void PurgeThumbnailCache()
        {
            _thumbnailService.ClearThumbCache();
        }

        public string AppVersionStr => string.Format(Properties.Resources.APP_VER, VersionDetection.GetCurrentVersion());

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
                }
            }
        }
        
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

        private bool _isMediaActive;

        public bool IsMediaActive
        {
            get => _isMediaActive;
            set
            {
                if (_isMediaActive != value)
                {
                    _isMediaActive = value;
                    RaisePropertyChanged();
                }
            }
        }

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
        
        public IEnumerable<MonitorItem> Monitors => _monitors;

        public string MonitorId
        {
            get => _optionsService.Options.MediaMonitorId;
            set
            {
                if (_optionsService.Options.MediaMonitorId != value)
                {
                    _optionsService.Options.MediaMonitorId = value;
                    RaisePropertyChanged();
                }
            }
        }

        private void OnShutDown(ShutDownMessage obj)
        {
            _optionsService.Save();
        }

        public IEnumerable<ImageFadeSpeed> FadeSpeedTypes => _fadingSpeeds;

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

        public IEnumerable<ImageFade> ImageFadeTypes => _fadingTypes;

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

        private IEnumerable<MonitorItem> GetSystemMonitors()
        {
            var result = new List<MonitorItem>
            {
                // empty (i.e. no timer monitor)
                new MonitorItem { MonitorName = Properties.Resources.MONITOR_NONE }
            };

            var monitors = _monitorsService.GetSystemMonitors();
            result.AddRange(monitors.Select(AutoMapper.Mapper.Map<MonitorItem>));

            return result;
        }

        public RelayCommand PurgeThumbnailCacheCommand { get; set; }
    }
}
