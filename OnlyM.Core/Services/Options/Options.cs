namespace OnlyM.Core.Services.Options
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Models;
    using Newtonsoft.Json;
    using Serilog.Events;
    using Utils;

    public sealed class Options
    {
        private const int AbsoluteMaxItemCount = 200;
        private const int DefaultMaxItemCount = 50;

        public event EventHandler MediaFolderChangedEvent;

        public event EventHandler ImageFadeTypeChangedEvent;

        public event EventHandler ImageFadeSpeedChangedEvent;

        public event EventHandler LogEventLevelChangedEvent;

        public event EventHandler AlwaysOnTopChangedEvent;

        public event EventHandler<MonitorChangedEventArgs> MediaMonitorChangedEvent;

        public event EventHandler RenderingMethodChangedEvent;

        public event EventHandler PermanentBackdropChangedEvent;

        public event EventHandler AllowVideoPauseChangedEvent;

        public event EventHandler AllowVideoPositionSeekingChangedEvent;

        public event EventHandler ShowSubtitlesChangedEvent;

        public event EventHandler UseInternalMediaTitlesChangedEvent;

        public event EventHandler IncludeBlankScreenItemChangedEvent;

        public event EventHandler VideoScreenPositionChangedEvent;

        public event EventHandler ImageScreenPositionChangedEvent;

        public event EventHandler ShowMediaItemCommandPanelChangedEvent;

        public event EventHandler OperatingDateChangedEvent;

        public event EventHandler MaxItemCountChangedEvent;

        public event EventHandler ShowFreezeCommandChangedEvent;


        public Options()
        {
            // defaults
            AlwaysOnTop = true;
            LogEventLevel = LogEventLevel.Information;
            MediaFolder = FileUtils.GetOnlyMDefaultMediaFolder();
            ImageFadeType = ImageFadeType.CrossFade;
            ImageFadeSpeed = FadeSpeed.Normal;
            CacheImages = true;
            ShowVideoSubtitles = true;
            AllowVideoScrubbing = true;
            AllowVideoPause = true;
            AllowVideoPositionSeeking = true;
            PermanentBackdrop = true;
            JwLibraryCompatibilityMode = true;
            ConfirmVideoStop = false;
            MaxItemCount = DefaultMaxItemCount;

            _videoScreenPosition = new ScreenPosition();
            _imageScreenPosition = new ScreenPosition();

            Sanitize();
        }

        private string _commandLineMediaFolder;

        public void SetCommandLineMediaFolder(string folder)
        {
            _commandLineMediaFolder = folder;
        }

        public bool IsCommandLineMediaFolderSpecified()
        {
            return _commandLineMediaFolder != null;
        }

        private bool _showMediaItemCommandPanel;

        public bool ShowMediaItemCommandPanel
        {
            get => _showMediaItemCommandPanel;
            set
            {
                if (_showMediaItemCommandPanel != value)
                {
                    _showMediaItemCommandPanel = value;
                    ShowMediaItemCommandPanelChangedEvent?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private bool _showFreezeCommand;

        public bool ShowFreezeCommand
        {
            get => _showFreezeCommand;
            set
            {
                if (_showFreezeCommand != value)
                {
                    _showFreezeCommand = value;
                    ShowFreezeCommandChangedEvent?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private string _mediaMonitorId;

        public string MediaMonitorId
        {
            get => _mediaMonitorId;
            set
            {
                if (_mediaMonitorId != value)
                {
                    var originalMonitorId = _mediaMonitorId;
                    _mediaMonitorId = value;
                    OnMediaMonitorChangedEvent(originalMonitorId, value);
                }
            }
        }

        private RenderingMethod _renderingMethod;

        public RenderingMethod RenderingMethod
        {
            get => _renderingMethod;
            set
            {
                if (_renderingMethod != value)
                {
                    _renderingMethod = value;
                    RenderingMethodChangedEvent?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private DateTime _operatingDate;

        [JsonIgnore]
        public DateTime OperatingDate
        {
            get => _operatingDate.Date;
            set
            {
                if (_operatingDate.Date != value.Date)
                {
                    _operatingDate = value.Date;
                    OperatingDateChangedEvent?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private int _maxItemCount;

        public int MaxItemCount
        {
            get => _maxItemCount;
            set
            {
                if (_maxItemCount != value)
                {
                    _maxItemCount = value;
                    MaxItemCountChangedEvent?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private ScreenPosition _videoScreenPosition;

        public ScreenPosition VideoScreenPosition
        {
            get => _videoScreenPosition;
            set
            {
                if (!_videoScreenPosition.SamePosition(value))
                {
                    _videoScreenPosition = value;
                    VideoScreenPositionChangedEvent?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private ScreenPosition _imageScreenPosition;

        public ScreenPosition ImageScreenPosition
        {
            get => _imageScreenPosition;
            set
            {
                if (!_imageScreenPosition.SamePosition(value))
                {
                    _imageScreenPosition = value;
                    ImageScreenPositionChangedEvent?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private bool _includeBlankScreenItem;

        public bool IncludeBlanksScreenItem
        {
            get => _includeBlankScreenItem;
            set
            {
                if (_includeBlankScreenItem != value)
                {
                    _includeBlankScreenItem = value;
                    IncludeBlankScreenItemChangedEvent?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private bool _useInternalMediaTitles;

        public bool UseInternalMediaTitles
        {
            get => _useInternalMediaTitles;
            set
            {
                if (_useInternalMediaTitles != value)
                {
                    _useInternalMediaTitles = value;
                    UseInternalMediaTitlesChangedEvent?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private bool _permanentBackdrop;

        public bool PermanentBackdrop
        {
            get => _permanentBackdrop;
            set
            {
                if (_permanentBackdrop != value)
                {
                    _permanentBackdrop = value;
                    PermanentBackdropChangedEvent?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private bool _allowVideoPause;

        public bool AllowVideoPause
        {
            get => _allowVideoPause;
            set
            {
                if (_allowVideoPause != value)
                {
                    _allowVideoPause = value;
                    AllowVideoPauseChangedEvent?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private bool _allowVideoPositionSeeking;

        public bool AllowVideoPositionSeeking
        {
            get => _allowVideoPositionSeeking;
            set
            {
                if (_allowVideoPositionSeeking != value)
                {
                    _allowVideoPositionSeeking = value;
                    AllowVideoPositionSeekingChangedEvent?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private bool _showVideoSubtitles;

        public bool ShowVideoSubtitles
        {
            get => _showVideoSubtitles;
            set
            {
                if (_showVideoSubtitles != value)
                {
                    _showVideoSubtitles = value;
                    ShowSubtitlesChangedEvent?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public bool AllowVideoScrubbing { get; set; }

        private bool _alwaysOnTop;

        public bool AlwaysOnTop
        {
            get => _alwaysOnTop;
            set
            {
                if (_alwaysOnTop != value)
                {
                    _alwaysOnTop = value;
                    AlwaysOnTopChangedEvent?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        
        public string AppWindowPlacement { get; set; }

        public bool JwLibraryCompatibilityMode { get; set; }

        public bool ConfirmVideoStop { get; set; }
        
        private LogEventLevel _logEventLevel;

        public LogEventLevel LogEventLevel
        {
            get => _logEventLevel;
            set
            {
                if (_logEventLevel != value)
                {
                    _logEventLevel = value;
                    LogEventLevelChangedEvent?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private string _mediaFolder;

        public string MediaFolder
        {
            get => _commandLineMediaFolder ?? _mediaFolder;
            set
            {
                if (_mediaFolder != value)
                {
                    _mediaFolder = value;
                    MediaFolderChangedEvent?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private ImageFadeType _imageFadeType;

        public ImageFadeType ImageFadeType
        {
            get => _imageFadeType;
            set
            {
                if (_imageFadeType != value)
                {
                    _imageFadeType = value;
                    ImageFadeTypeChangedEvent?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private FadeSpeed _fadeSpeed;

        public FadeSpeed ImageFadeSpeed
        {
            get => _fadeSpeed;
            set
            {
                if (_fadeSpeed != value)
                {
                    _fadeSpeed = value;
                    ImageFadeSpeedChangedEvent?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public bool EmbeddedThumbnails { get; set; }

        public bool CacheImages { get; set; }

        public List<string> RecentlyUsedMediaFolders { get; set; } = new List<string>();

        /// <summary>
        /// Validates the data, correcting automatically as required
        /// </summary>
        public void Sanitize()
        {
            if (!Directory.Exists(MediaFolder))
            {
                MediaFolder = FileUtils.GetOnlyMDefaultMediaFolder();
            }

            if (JwLibraryCompatibilityMode)
            {
                PermanentBackdrop = false;
            }

            VideoScreenPosition.Sanitize();
            ImageScreenPosition.Sanitize();

            if (!RecentlyUsedMediaFolders.Any())
            {
                RecentlyUsedMediaFolders.Add(!string.IsNullOrEmpty(MediaFolder)
                    ? MediaFolder
                    : FileUtils.GetOnlyMDefaultMediaFolder());
            }

            for (int n = RecentlyUsedMediaFolders.Count - 1; n >= 0; --n)
            {
                var folder = RecentlyUsedMediaFolders[n];
                if (!Directory.Exists(folder))
                {
                    RecentlyUsedMediaFolders.RemoveAt(n);
                }
            }

            // media calendar date is always set to today
            // on startup.
            _operatingDate = DateTime.Today;

            if (MaxItemCount > AbsoluteMaxItemCount)
            {
                MaxItemCount = AbsoluteMaxItemCount;
            }

            if (MaxItemCount <= 0)
            {
                MaxItemCount = 1;
            }
        }

        private void OnMediaMonitorChangedEvent(string originalMonitorId, string newMonitorId)
        {
            MediaMonitorChangedEvent?.Invoke(
                this, 
                new MonitorChangedEventArgs
                {
                    OriginalMonitorId = originalMonitorId,
                    NewMonitorId = newMonitorId
                });
        }
    }
}
