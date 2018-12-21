namespace OnlyM.Core.Services.Options
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Windows;
    using System.Windows.Markup;
    using CommandLine;
    using CommonServiceLocator;
    using Models;
    using Monitors;
    using Newtonsoft.Json;
    using Serilog;
    using Serilog.Events;
    using Utils;

    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed class OptionsService : IOptionsService
    {
        private readonly ILogLevelSwitchService _logLevelSwitchService;
        private readonly ICommandLineService _commandLineService;

        private readonly int _optionsVersion = 1;
        private readonly Lazy<Options> _options;

        private string _optionsFilePath;
        private string _originalOptionsSignature;
        private string _commandLineMediaFolder;

        public OptionsService(
            ILogLevelSwitchService logLevelSwitchService,
            ICommandLineService commandLineService)
        {
            _options = new Lazy<Options>(OptionsFactory);

            _logLevelSwitchService = logLevelSwitchService;
            _commandLineService = commandLineService;
        }

        public event EventHandler MediaFolderChangedEvent;

        public event EventHandler AutoRotateChangedEvent;

        public event EventHandler ImageFadeTypeChangedEvent;

        public event EventHandler ImageFadeSpeedChangedEvent;

        public event EventHandler AlwaysOnTopChangedEvent;

        public event EventHandler<MonitorChangedEventArgs> MediaMonitorChangedEvent;

        public event EventHandler RenderingMethodChangedEvent;

        public event EventHandler PermanentBackdropChangedEvent;

        public event EventHandler AllowVideoPauseChangedEvent;

        public event EventHandler AllowVideoPositionSeekingChangedEvent;

        public event EventHandler ShowSubtitlesChangedEvent;

        public event EventHandler UseInternalMediaTitlesChangedEvent;

        public event EventHandler IncludeBlankScreenItemChangedEvent;

        public event EventHandler AllowMirrorChangedEvent;

        public event EventHandler VideoScreenPositionChangedEvent;

        public event EventHandler ImageScreenPositionChangedEvent;

        public event EventHandler WebScreenPositionChangedEvent;

        public event EventHandler ShowMediaItemCommandPanelChangedEvent;

        public event EventHandler OperatingDateChangedEvent;

        public event EventHandler MaxItemCountChangedEvent;

        public event EventHandler ShowFreezeCommandChangedEvent;

        public event EventHandler MagnifierChangedEvent;

        public event EventHandler BrowserChangedEvent;
        
        public bool ShouldPurgeBrowserCacheOnStartup
        {
            get => _options.Value.ShouldPurgeBrowserCacheOnStartup;
            set => _options.Value.ShouldPurgeBrowserCacheOnStartup = value;
        }

        public string AppWindowPlacement
        {
            get => _options.Value.AppWindowPlacement;
            set
            {
                if (_options.Value.AppWindowPlacement != value)
                {
                    _options.Value.AppWindowPlacement = value;
                }
            }
        }

        public List<string> RecentlyUsedMediaFolders
        {
            get => _options.Value.RecentlyUsedMediaFolders;
            set => _options.Value.RecentlyUsedMediaFolders = value;
        }

        public string Culture
        {
            get => _options.Value.Culture;
            set
            {
                if (_options.Value.Culture != value)
                {
                    _options.Value.Culture = value;
                }
            }
        }

        public bool CacheImages
        {
            get => _options.Value.CacheImages;
            set
            {
                if (_options.Value.CacheImages != value)
                {
                    _options.Value.CacheImages = value;
                }
            }
        }

        public bool EmbeddedThumbnails
        {
            get => _options.Value.EmbeddedThumbnails;
            set
            {
                if (_options.Value.EmbeddedThumbnails != value)
                {
                    _options.Value.EmbeddedThumbnails = value;
                }
            }
        }

        public bool ConfirmVideoStop
        {
            get => _options.Value.ConfirmVideoStop;
            set
            {
                if (_options.Value.ConfirmVideoStop != value)
                {
                    _options.Value.ConfirmVideoStop = value;
                }
            }
        }

        public bool JwLibraryCompatibilityMode
        {
            get => _options.Value.JwLibraryCompatibilityMode;
            set
            {
                if (_options.Value.JwLibraryCompatibilityMode != value)
                {
                    _options.Value.JwLibraryCompatibilityMode = value;
                }
            } 
        }

        public bool AllowVideoScrubbing
        {
            get => _options.Value.AllowVideoScrubbing;
            set
            {
                if (_options.Value.AllowVideoScrubbing != value)
                {
                    _options.Value.AllowVideoScrubbing = value;
                }
            }
        }

        public bool ShowFreezeCommand
        {
            get => _options.Value.ShowFreezeCommand;
            set
            {
                if (_options.Value.ShowFreezeCommand != value)
                {
                    _options.Value.ShowFreezeCommand = value;
                    ShowFreezeCommandChangedEvent?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public int MaxItemCount
        {
            get => _options.Value.MaxItemCount;
            set
            {
                if (_options.Value.MaxItemCount != value)
                {
                    _options.Value.MaxItemCount = value;
                    MaxItemCountChangedEvent?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public DateTime OperatingDate
        {
            get => _options.Value.OperatingDate.Date;
            set
            {
                if (_options.Value.OperatingDate.Date != value.Date)
                {
                    _options.Value.OperatingDate = value.Date;
                    OperatingDateChangedEvent?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public bool ShowMediaItemCommandPanel
        {
            get => _options.Value.ShowMediaItemCommandPanel;
            set
            {
                if (_options.Value.ShowMediaItemCommandPanel != value)
                {
                    _options.Value.ShowMediaItemCommandPanel = value;
                    ShowMediaItemCommandPanelChangedEvent?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public ScreenPosition VideoScreenPosition
        {
            get => _options.Value.VideoScreenPosition;
            set
            {
                if (!_options.Value.VideoScreenPosition.SamePosition(value))
                {
                    _options.Value.VideoScreenPosition = value;
                    VideoScreenPositionChangedEvent?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public ScreenPosition ImageScreenPosition
        {
            get => _options.Value.ImageScreenPosition;
            set
            {
                if (!_options.Value.ImageScreenPosition.SamePosition(value))
                {
                    _options.Value.ImageScreenPosition = value;
                    ImageScreenPositionChangedEvent?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public ScreenPosition WebScreenPosition
        {
            get => _options.Value.WebScreenPosition;
            set
            {
                if (!_options.Value.WebScreenPosition.SamePosition(value))
                {
                    _options.Value.WebScreenPosition = value;
                    WebScreenPositionChangedEvent?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public bool AllowMirror
        {
            get => _options.Value.AllowMirror;
            set
            {
                if (_options.Value.AllowMirror != value)
                {
                    _options.Value.AllowMirror = value;
                    AllowMirrorChangedEvent?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public bool UseMirrorByDefault
        {
            get => _options.Value.UseMirrorByDefault;
            set
            {
                if (_options.Value.UseMirrorByDefault != value)
                {
                    _options.Value.UseMirrorByDefault = value;
                }
            }
        }

        public char MirrorHotKey
        {
            get => _options.Value.MirrorHotKey;
            set
            {
                if (_options.Value.MirrorHotKey != value)
                {
                    _options.Value.MirrorHotKey = value;
                }
            }
        }

        public double MirrorZoom
        {
            get => _options.Value.MirrorZoom;
            set
            {
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (_options.Value.MirrorZoom != value)
                {
                    _options.Value.MirrorZoom = value;
                }
            }
        }
        
        public bool IncludeBlankScreenItem
        {
            get => _options.Value.IncludeBlankScreenItem;
            set
            {
                if (_options.Value.IncludeBlankScreenItem != value)
                {
                    _options.Value.IncludeBlankScreenItem = value;
                    IncludeBlankScreenItemChangedEvent?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public bool UseInternalMediaTitles
        {
            get => _options.Value.UseInternalMediaTitles;
            set
            {
                if (_options.Value.UseInternalMediaTitles != value)
                {
                    _options.Value.UseInternalMediaTitles = value;
                    UseInternalMediaTitlesChangedEvent?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public bool ShowVideoSubtitles
        {
            get => _options.Value.ShowVideoSubtitles;
            set
            {
                if (_options.Value.ShowVideoSubtitles != value)
                {
                    _options.Value.ShowVideoSubtitles = value;
                    ShowSubtitlesChangedEvent?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public bool AllowVideoPositionSeeking
        {
            get => _options.Value.AllowVideoPositionSeeking;
            set
            {
                if (_options.Value.AllowVideoPositionSeeking != value)
                {
                    _options.Value.AllowVideoPositionSeeking = value;
                    AllowVideoPositionSeekingChangedEvent?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public bool AllowVideoPause
        {
            get => _options.Value.AllowVideoPause;
            set
            {
                if (_options.Value.AllowVideoPause != value)
                {
                    _options.Value.AllowVideoPause = value;
                    AllowVideoPauseChangedEvent?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public bool PermanentBackdrop
        {
            get => _options.Value.PermanentBackdrop;
            set
            {
                if (_options.Value.PermanentBackdrop != value)
                {
                    _options.Value.PermanentBackdrop = value;
                    PermanentBackdropChangedEvent?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public RenderingMethod RenderingMethod
        {
            get => _options.Value.RenderingMethod;
            set
            {
                if (_options.Value.RenderingMethod != value)
                {
                    _options.Value.RenderingMethod = value;
                    RenderingMethodChangedEvent?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public string MediaMonitorId
        {
            get => _options.Value.MediaMonitorId;
            set
            {
                if (_options.Value.MediaMonitorId != value)
                {
                    var originalMonitorId = _options.Value.MediaMonitorId;
                    _options.Value.MediaMonitorId = value;
                    OnMediaMonitorChangedEvent(originalMonitorId, value);
                }
            }
        }

        public double BrowserZoomLevelIncrement
        {
            get => _options.Value.BrowserZoomLevelIncrement;
            set
            {
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (_options.Value.BrowserZoomLevelIncrement != value)
                {
                    _options.Value.BrowserZoomLevelIncrement = value;
                    BrowserChangedEvent?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public LogEventLevel LogEventLevel
        {
            get => _options.Value.LogEventLevel;
            set
            {
                if (_options.Value.LogEventLevel != value)
                {
                    _options.Value.LogEventLevel = value;
                    _logLevelSwitchService.SetMinimumLevel(value);
                }
            }
        }

        public bool AlwaysOnTop
        {
            get => _options.Value.AlwaysOnTop;
            set
            {
                if (_options.Value.AlwaysOnTop != value)
                {
                    _options.Value.AlwaysOnTop = value;
                    AlwaysOnTopChangedEvent?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public double MagnifierFrameThickness
        {
            get => _options.Value.MagnifierFrameThickness;
            set
            {
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (_options.Value.MagnifierFrameThickness != value)
                {
                    _options.Value.MagnifierFrameThickness = value;
                    MagnifierChangedEvent?.Invoke(this, EventArgs.Empty);
                }
            } 
        }

        public MagnifierShape MagnifierShape
        {
            get => _options.Value.MagnifierShape;
            set
            {
                if (_options.Value.MagnifierShape != value)
                {
                    _options.Value.MagnifierShape = value;
                    MagnifierChangedEvent?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public MagnifierSize MagnifierSize
        {
            get => _options.Value.MagnifierSize;
            set
            {
                if (_options.Value.MagnifierSize != value)
                {
                    _options.Value.MagnifierSize = value;
                    MagnifierChangedEvent?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        
        public double MagnifierZoomLevel
        {
            get => _options.Value.MagnifierZoomLevel;
            set
            {
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (_options.Value.MagnifierZoomLevel != value)
                {
                    _options.Value.MagnifierZoomLevel = value;
                    MagnifierChangedEvent?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public FadeSpeed ImageFadeSpeed
        {
            get => _options.Value.ImageFadeSpeed;
            set
            {
                if (_options.Value.ImageFadeSpeed != value)
                {
                    _options.Value.ImageFadeSpeed = value;
                    ImageFadeSpeedChangedEvent?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public ImageFadeType ImageFadeType
        {
            get => _options.Value.ImageFadeType;
            set
            {
                if (_options.Value.ImageFadeType != value)
                {
                    _options.Value.ImageFadeType = value;
                    ImageFadeTypeChangedEvent?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public bool AutoRotateImages
        {
            get => _options.Value.AutoRotateImages;
            set
            {
                if (_options.Value.AutoRotateImages != value)
                {
                    _options.Value.AutoRotateImages = value;
                    AutoRotateChangedEvent?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public string MediaFolder
        {
            get => _commandLineMediaFolder ?? _options.Value.MediaFolder;
            set
            {
                if (_options.Value.MediaFolder != value)
                {
                    _options.Value.MediaFolder = value;
                    MediaFolderChangedEvent?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public bool IsMediaMonitorSpecified => !string.IsNullOrEmpty(_options.Value.MediaMonitorId);

        public void SetCommandLineMediaFolder(string folder)
        {
            _commandLineMediaFolder = folder;
        }

        public bool IsCommandLineMediaFolderSpecified()
        {
            return _commandLineMediaFolder != null;
        }

        /// <summary>
        /// Saves the settings (if they have changed since they were last read)
        /// </summary>
        public void Save()
        {
            try
            {
                ClearCommandLineMediaFolderOverride();

                var newSignature = GetOptionsSignature(_options.Value);
                if (_originalOptionsSignature != newSignature)
                {
                    // changed...
                    WriteOptions(_options.Value);
                    Log.Logger.Debug("Settings changed and saved");
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Could not save settings");
            }
            finally
            {
                SetCommandLineMediaFolderOverride();
            }
        }

        private void SetCommandLineMediaFolderOverride()
        {
            var commandLineMediaFolder = _commandLineService.SourceFolder;
            if (!string.IsNullOrEmpty(commandLineMediaFolder) && Directory.Exists(commandLineMediaFolder))
            {
                SetCommandLineMediaFolder(commandLineMediaFolder);
            }
        }

        private void ClearCommandLineMediaFolderOverride()
        {
            SetCommandLineMediaFolder(null);
        }

        private string GetOptionsSignature(Options options)
        {
            // config data is small so simple solution is best...
            return JsonConvert.SerializeObject(options);
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

        private Options OptionsFactory()
        {
            Options result = null;

            try
            {
                var commandLineIdentifier = _commandLineService.OptionsIdentifier;
                _optionsFilePath = FileUtils.GetUserOptionsFilePath(commandLineIdentifier, _optionsVersion);
                var path = Path.GetDirectoryName(_optionsFilePath);
                if (path != null)
                {
                    FileUtils.CreateDirectory(path);
                    result = ReadOptions();
                }

                if (result == null)
                {
                    result = new Options();
                }

                // store the original settings so that we can determine if they have changed
                // when we come to save them
                _originalOptionsSignature = GetOptionsSignature(result);

                SetCommandLineMediaFolderOverride();
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Could not read options file");
                result = new Options();
            }

            _logLevelSwitchService.SetMinimumLevel(result.LogEventLevel);

            return result;
        }

        private Options ReadOptions()
        {
            if (!File.Exists(_optionsFilePath))
            {
                return WriteDefaultOptions();
            }

            using (var file = File.OpenText(_optionsFilePath))
            {
                var serializer = new JsonSerializer();
                var result = (Options)serializer.Deserialize(file, typeof(Options));
                result.Sanitize();

                SetCulture(result.Culture);

                return result;
            }
        }

        private void SetCulture(string cultureString)
        {
            var culture = cultureString;

            if (string.IsNullOrEmpty(culture))
            {
                culture = CultureInfo.CurrentCulture.Name;
            }

            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo(culture);
                Thread.CurrentThread.CurrentUICulture = new CultureInfo(culture);
                FrameworkElement.LanguageProperty.OverrideMetadata(
                    typeof(FrameworkElement),
                    new FrameworkPropertyMetadata(
                        XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Could not set culture");
            }
        }

        private Options WriteDefaultOptions()
        {
            var result = new Options();

            // first time launched so set the monitor to the first one we find
            var monitorService = ServiceLocator.Current.GetInstance<IMonitorsService>();
            result.MediaMonitorId = monitorService.GetSystemMonitors().First().MonitorId;

            WriteOptions(result);

            return result;
        }

        private void WriteOptions(Options options)
        {
            if (options != null)
            {
                using (var file = File.CreateText(_optionsFilePath))
                {
                    var serializer = new JsonSerializer { Formatting = Formatting.Indented };
                    serializer.Serialize(file, options);
                    _originalOptionsSignature = GetOptionsSignature(options);
                }
            }
        }
    }
}
