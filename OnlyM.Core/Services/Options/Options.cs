namespace OnlyM.Core.Services.Options
{
    using System;
    using System.IO;
    using Models;
    using Serilog.Events;
    using Utils;

    public sealed class Options
    {
        public event EventHandler MediaFolderChangedEvent;

        public event EventHandler ImageFadeTypeChangedEvent;

        public event EventHandler ImageFadeSpeedChangedEvent;

        public event EventHandler LogEventLevelChangedEvent;

        public event EventHandler AlwaysOnTopChangedEvent;

        public event EventHandler MediaMonitorChangedEvent;

        public event EventHandler PermanentBackdropChangedEvent;

        public event EventHandler AllowVideoPauseChangedEvent;

        public event EventHandler AllowVideoPositionSeekingChangedEvent;

        public event EventHandler ShowSubtitlesChangedEvent;

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
        }

        private string _mediaMonitorId;

        public string MediaMonitorId
        {
            get => _mediaMonitorId;
            set
            {
                if (_mediaMonitorId != value)
                {
                    _mediaMonitorId = value;
                    OnMediaMonitorChangedEvent();
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
                    OnPermanentBackdropChangedEvent();
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
                    OnAllowPauseChangedEvent();
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
                    OnAllowPositionSeekingChangedEvent();
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
                    OnShowSubtitlesChangedEvent();
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
                    OnAlwaysOnTopChangedEvent();
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
                    OnLogEventLevelChangedEvent();
                }
            }
        }

        private string _mediaFolder;

        public string MediaFolder
        {
            get => _mediaFolder;
            set
            {
                if (_mediaFolder != value)
                {
                    _mediaFolder = value;
                    OnMediaFolderChangedEvent();
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
                    OnImageFadeTypeChangedEvent();
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
                    OnImageFadeSpeedChangedEvent();
                }
            }
        }

        public bool EmbeddedThumbnails { get; set; }

        public bool CacheImages { get; set; }

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
        }

        private void OnMediaFolderChangedEvent()
        {
            MediaFolderChangedEvent?.Invoke(this, EventArgs.Empty);
        }

        private void OnImageFadeTypeChangedEvent()
        {
            ImageFadeTypeChangedEvent?.Invoke(this, EventArgs.Empty);
        }

        private void OnImageFadeSpeedChangedEvent()
        {
            ImageFadeSpeedChangedEvent?.Invoke(this, EventArgs.Empty);
        }

        private void OnLogEventLevelChangedEvent()
        {
            LogEventLevelChangedEvent?.Invoke(this, EventArgs.Empty);
        }

        private void OnAlwaysOnTopChangedEvent()
        {
            AlwaysOnTopChangedEvent?.Invoke(this, EventArgs.Empty);
        }

        private void OnMediaMonitorChangedEvent()
        {
            MediaMonitorChangedEvent?.Invoke(this, EventArgs.Empty);
        }

        private void OnPermanentBackdropChangedEvent()
        {
            PermanentBackdropChangedEvent?.Invoke(this, EventArgs.Empty);
        }

        private void OnAllowPauseChangedEvent()
        {
            AllowVideoPauseChangedEvent?.Invoke(this, EventArgs.Empty);
        }

        private void OnAllowPositionSeekingChangedEvent()
        {
            AllowVideoPositionSeekingChangedEvent?.Invoke(this, EventArgs.Empty);
        }

        private void OnShowSubtitlesChangedEvent()
        {
            ShowSubtitlesChangedEvent?.Invoke(this, EventArgs.Empty);
        }
    }
}
