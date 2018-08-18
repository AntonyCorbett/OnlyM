namespace OnlyM.Core.Services.Options
{
    using System;
    using System.IO;
    using System.Linq;
    using CommandLine;
    using CommonServiceLocator;
    using Models;
    using Monitors;
    using Newtonsoft.Json;
    using Serilog;
    using Utils;

    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed class OptionsService : IOptionsService
    {
        private readonly ILogLevelSwitchService _logLevelSwitchService;
        private readonly ICommandLineService _commandLineService;

        private readonly int _optionsVersion = 1;
        private Options _options;
        private string _optionsFilePath;
        private string _originalOptionsSignature;
       
        public event EventHandler MediaFolderChangedEvent;

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

        public event EventHandler VideoScreenPositionChangedEvent;

        public event EventHandler ImageScreenPositionChangedEvent;

        public event EventHandler ShowMediaItemCommandPanelChangedEvent;

        public event EventHandler OperatingDateChangedEvent;

        public event EventHandler MaxItemCountChangedEvent;

        public event EventHandler ShowFreezeCommandChangedEvent;

        public OptionsService(
            ILogLevelSwitchService logLevelSwitchService,
            ICommandLineService commandLineService)
        {
            _logLevelSwitchService = logLevelSwitchService;
            _commandLineService = commandLineService;
        }

        public Options Options
        {
            get
            {
                Init();
                return _options;
            }
        }

        public bool IsMediaMonitorSpecified
        {
            get
            {
                Init();
                return !string.IsNullOrEmpty(Options.MediaMonitorId);
            }
        }

        /// <summary>
        /// Saves the settings (if they have changed since they were last read)
        /// </summary>
        public void Save()
        {
            try
            {
                ClearCommandLineMediaFolderOverride();

                var newSignature = GetOptionsSignature(_options);
                if (_originalOptionsSignature != newSignature)
                {
                    // changed...
                    WriteOptions();
                    Log.Logger.Debug("Settings changed and saved");
                }
            }
            // ReSharper disable once CatchAllClause
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Could not save settings");
            }
            finally
            {
                SetCommandLineMediaFolderOverride();
            }
        }

        private void Init()
        {
            if (_options == null)
            {
                try
                {
                    string commandLineIdentifier = _commandLineService.OptionsIdentifier;
                    _optionsFilePath = FileUtils.GetUserOptionsFilePath(commandLineIdentifier, _optionsVersion);
                    var path = Path.GetDirectoryName(_optionsFilePath);
                    if (path != null)
                    {
                        FileUtils.CreateDirectory(path);
                        ReadOptions();
                    }

                    if (_options == null)
                    {
                        _options = new Options();
                    }
                    
                    // store the original settings so that we can determine if they have changed
                    // when we come to save them
                    _originalOptionsSignature = GetOptionsSignature(_options);
                    
                    SetCommandLineMediaFolderOverride();
                }
                // ReSharper disable once CatchAllClause
                catch (Exception ex)
                {
                    Log.Logger.Error(ex, "Could not read options file");
                    _options = new Options();
                }

                if (_options != null)
                {
                    _options.MediaFolderChangedEvent += HandleMediaFolderChangedEvent;
                    _options.ImageFadeTypeChangedEvent += HandleImageFadeTypeChangedEvent;
                    _options.ImageFadeSpeedChangedEvent += HandleImageFadeSpeedChangedEvent;
                    _options.LogEventLevelChangedEvent += HandleLogEventLevelChangedEvent;
                    _options.AlwaysOnTopChangedEvent += HandleAlwaysOnTopChangedEvent;
                    _options.MediaMonitorChangedEvent += HandleMediaMonitorChangedEvent;
                    _options.RenderingMethodChangedEvent += HandleRenderingMethodChangedEvent;
                    _options.PermanentBackdropChangedEvent += HandlePermanentBackdropChangedEvent;
                    _options.AllowVideoPauseChangedEvent += HandleAllowVideoPauseChangedEvent;
                    _options.AllowVideoPositionSeekingChangedEvent += HandleAllowVideoPositionSeekingChangedEvent;
                    _options.ShowSubtitlesChangedEvent += HandleShowSubtitlesChangedEvent;
                    _options.UseInternalMediaTitlesChangedEvent += HandleUseInternalMediaTitlesChangedEvent;
                    _options.IncludeBlankScreenItemChangedEvent += HandleIncludeBlankScreenItemChangedEvent;
                    _options.VideoScreenPositionChangedEvent += HandleVideoScreenPositionChangedEvent;
                    _options.ImageScreenPositionChangedEvent += HandleImageScreenPositionChangedEvent;
                    _options.ShowMediaItemCommandPanelChangedEvent += HandleShowMediaItemCommandPanelChangedEvent;
                    _options.OperatingDateChangedEvent += HandleOperatingDateChangedEvent;
                    _options.MaxItemCountChangedEvent += HandleMaxItemCountChangedEvent;
                    _options.ShowFreezeCommandChangedEvent += HandleShowFreezeCommandChangedEvent;

                    _logLevelSwitchService.SetMinimumLevel(Options.LogEventLevel);
                }
            }
        }

        private void SetCommandLineMediaFolderOverride()
        {
            string commandLineMediaFolder = _commandLineService.SourceFolder;
            if (!string.IsNullOrEmpty(commandLineMediaFolder) && Directory.Exists(commandLineMediaFolder))
            {
                _options.SetCommandLineMediaFolder(commandLineMediaFolder);
            }
        }

        private void ClearCommandLineMediaFolderOverride()
        {
            _options.SetCommandLineMediaFolder(null);
        }

        private void HandleShowFreezeCommandChangedEvent(object sender, EventArgs e)
        {
            ShowFreezeCommandChangedEvent?.Invoke(this, e);
        }

        private void HandleMaxItemCountChangedEvent(object sender, EventArgs e)
        {
            MaxItemCountChangedEvent?.Invoke(this, e);
        }

        private void HandleOperatingDateChangedEvent(object sender, EventArgs e)
        {
            OperatingDateChangedEvent?.Invoke(this, e);
        }

        private void HandleShowMediaItemCommandPanelChangedEvent(object sender, EventArgs e)
        {
            ShowMediaItemCommandPanelChangedEvent?.Invoke(this, e);
        }

        private void HandleImageScreenPositionChangedEvent(object sender, EventArgs e)
        {
            ImageScreenPositionChangedEvent?.Invoke(this, e);
        }

        private void HandleVideoScreenPositionChangedEvent(object sender, EventArgs e)
        {
            VideoScreenPositionChangedEvent?.Invoke(this, e);
        }

        private void HandleIncludeBlankScreenItemChangedEvent(object sender, EventArgs e)
        {
            IncludeBlankScreenItemChangedEvent?.Invoke(this, e);
        }

        private void HandleUseInternalMediaTitlesChangedEvent(object sender, EventArgs e)
        {
            UseInternalMediaTitlesChangedEvent?.Invoke(this, e);
        }

        private void HandleShowSubtitlesChangedEvent(object sender, EventArgs e)
        {
            ShowSubtitlesChangedEvent?.Invoke(this, e);
        }

        private void HandleAllowVideoPositionSeekingChangedEvent(object sender, EventArgs e)
        {
            AllowVideoPositionSeekingChangedEvent?.Invoke(this, e);
        }

        private void HandleAllowVideoPauseChangedEvent(object sender, EventArgs e)
        {
            AllowVideoPauseChangedEvent?.Invoke(this, e);
        }

        private void HandlePermanentBackdropChangedEvent(object sender, EventArgs e)
        {
            PermanentBackdropChangedEvent?.Invoke(this, e);
        }

        private void HandleMediaMonitorChangedEvent(object sender, MonitorChangedEventArgs e)
        {
            MediaMonitorChangedEvent?.Invoke(this, e);
        }

        private void HandleRenderingMethodChangedEvent(object sender, EventArgs e)
        {
            RenderingMethodChangedEvent?.Invoke(this, e);
        }

        private void HandleAlwaysOnTopChangedEvent(object sender, EventArgs e)
        {
            AlwaysOnTopChangedEvent?.Invoke(this, EventArgs.Empty);
        }

        private void HandleImageFadeSpeedChangedEvent(object sender, EventArgs e)
        {
            ImageFadeSpeedChangedEvent?.Invoke(this, EventArgs.Empty);
        }

        private void HandleLogEventLevelChangedEvent(object sender, EventArgs e)
        {
            _logLevelSwitchService.SetMinimumLevel(Options.LogEventLevel);
        }

        private void HandleImageFadeTypeChangedEvent(object sender, EventArgs e)
        {
            ImageFadeTypeChangedEvent?.Invoke(this, EventArgs.Empty);
        }

        private void HandleMediaFolderChangedEvent(object sender, EventArgs e)
        {
            MediaFolderChangedEvent?.Invoke(this, EventArgs.Empty);
        }

        private string GetOptionsSignature(Options options)
        {
            // config data is small so simple solution is best...
            return JsonConvert.SerializeObject(options);
        }

        private void ReadOptions()
        {
            if (!File.Exists(_optionsFilePath))
            {
                WriteDefaultOptions();
            }
            else
            {
                using (StreamReader file = File.OpenText(_optionsFilePath))
                {
                    var serializer = new JsonSerializer();
                    _options = (Options)serializer.Deserialize(file, typeof(Options));
                    _options.Sanitize();
                }
            }
        }

        private void WriteDefaultOptions()
        {
            _options = new Options();

            // first time launched so set the monitor to the first one we find
            var monitorService = ServiceLocator.Current.GetInstance<IMonitorsService>();
            _options.MediaMonitorId = monitorService.GetSystemMonitors().First().MonitorId;

            WriteOptions();
        }

        private void WriteOptions()
        {
            if (_options != null)
            {
                using (var file = File.CreateText(_optionsFilePath))
                {
                    var serializer = new JsonSerializer { Formatting = Formatting.Indented };
                    serializer.Serialize(file, _options);
                    _originalOptionsSignature = GetOptionsSignature(_options);
                }
            }
        }
    }
}
