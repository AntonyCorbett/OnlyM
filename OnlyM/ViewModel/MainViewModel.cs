namespace OnlyM.ViewModel
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Interop;
    using System.Windows.Media;
    using GalaSoft.MvvmLight;
    using GalaSoft.MvvmLight.CommandWpf;
    using GalaSoft.MvvmLight.Messaging;
    using MaterialDesignThemes.Wpf;
    using OnlyM.AutoUpdates;
    using OnlyM.Core.Models;
    using OnlyM.Core.Services.CommandLine;
    using OnlyM.Core.Services.Options;
    using OnlyM.Core.Utils;
    using OnlyM.CoreSys.Services.Snackbar;
    using OnlyM.Models;
    using OnlyM.PubSubMessages;
    using OnlyM.Services.DragAndDrop;
    using OnlyM.Services.HiddenMediaItems;
    using OnlyM.Services.MediaChanging;
    using OnlyM.Services.Pages;
    using Serilog;
    using Serilog.Events;
    
    // ReSharper disable once ClassNeverInstantiated.Global
    internal class MainViewModel : ViewModelBase
    {
        private readonly IPageService _pageService;
        private readonly IOptionsService _optionsService;
        private readonly ISnackbarService _snackbarService;
        private readonly IMediaStatusChangingService _mediaStatusChangingService;
        private readonly IHiddenMediaItemsService _hiddenMediaItemsService;
        private readonly ICommandLineService _commandLineService;

        private bool _isBusy;
        private bool _isMediaListLoading;
        private string _currentPageName;
        private FrameworkElement _currentPage;
        private bool _newVersionAvailable;
        private bool _isMediaListEmpty = true;

        public MainViewModel(
            IPageService pageService,
            IOptionsService optionsService,
            ISnackbarService snackbarService,
            IMediaStatusChangingService mediaStatusChangingService,
            IHiddenMediaItemsService hiddenMediaItemsService,
            ICommandLineService commandLineService,
            IDragAndDropService dragAndDropService)
        {
            _commandLineService = commandLineService;
            
            if (commandLineService.NoGpu || ForceSoftwareRendering())
            {
                // disable hardware (GPU) rendering so that it's all done by the CPU...
                RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
            }

            Messenger.Default.Register<MediaListUpdatingMessage>(this, OnMediaListUpdating);
            Messenger.Default.Register<MediaListUpdatedMessage>(this, OnMediaListUpdated);

            _mediaStatusChangingService = mediaStatusChangingService;
            _hiddenMediaItemsService = hiddenMediaItemsService;

            _hiddenMediaItemsService.HiddenItemsChangedEvent += HandleHiddenItemsChangedEvent;

            _pageService = pageService;
            _pageService.NavigationEvent += HandlePageNavigationEvent;
            _pageService.MediaMonitorChangedEvent += HandleMediaMonitorChangedEvent;
            _pageService.MediaWindowVisibilityChanged += HandleMediaWindowVisibilityChangedEvent;
            
            _snackbarService = snackbarService;
            
            _optionsService = optionsService;
            _optionsService.AlwaysOnTopChangedEvent += HandleAlwaysOnTopChangedEvent;

            if (_optionsService.ShouldPurgeBrowserCacheOnStartup)
            {
                _optionsService.ShouldPurgeBrowserCacheOnStartup = false;
                _optionsService.Save();
                FileUtils.DeleteBrowserCacheFolder();
            }

            _pageService.GotoOperatorPage();

            dragAndDropService.CopyingFilesProgressEvent += HandleCopyingFilesProgressEvent;

            InitCommands();

            if (!IsInDesignMode)
            {
                _pageService.InitMediaWindow();
            }

            SanityChecks();
        }

        // commands...
        public RelayCommand GotoSettingsCommand { get; set; }

        public RelayCommand GotoOperatorCommand { get; set; }

        public RelayCommand LaunchMediaFolderCommand { get; set; }

        public RelayCommand LaunchHelpPageCommand { get; set; }

        public RelayCommand LaunchReleasePageCommand { get; set; }

        public RelayCommand UnhideCommand { get; set; }

        public ISnackbarMessageQueue TheSnackbarMessageQueue => _snackbarService.TheSnackbarMessageQueue;

        public bool ShowNewVersionButton => _newVersionAvailable && IsOperatorPageActive;

        public bool AlwaysOnTop => _optionsService.AlwaysOnTop || _pageService.IsMediaWindowVisible;

        public bool IsSettingsPageActive => _currentPageName.Equals(_pageService.SettingsPageName);

        public bool IsOperatorPageActive => _currentPageName.Equals(_pageService.OperatorPageName);

        public bool IsSettingsEnabled => !_commandLineService.NoSettings;

        public bool IsFolderEnabled => !_commandLineService.NoFolder;

        public string SettingsHint =>
            _commandLineService.NoSettings
                ? Properties.Resources.SETTINGS_DISABLED
                : Properties.Resources.SETTINGS;

        public string FolderHint =>
            _commandLineService.NoFolder
                ? Properties.Resources.FOLDER_DISABLED
                : Properties.Resources.FOLDER;

        public bool IsMediaListEmpty
        {
            get => _isMediaListEmpty;
            set
            {
                if (_isMediaListEmpty != value)
                {
                    _isMediaListEmpty = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(ShowDragAndDropHint));
                }
            }
        }

        public bool IsUnhideButtonVisible =>
            IsInDesignMode || (IsOperatorPageActive && !ShowProgressBar && _hiddenMediaItemsService.SomeHiddenMediaItems());

        public bool ShowProgressBar => IsBusy;

        public bool ShowDragAndDropHint => IsMediaListEmpty && IsOperatorPageActive;

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (_isBusy != value)
                {
                    _isBusy = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(IsUnhideButtonVisible));
                    RaisePropertyChanged(nameof(ShowProgressBar));
                }
            }
        }

        public FrameworkElement CurrentPage
        {
            get => _currentPage;
            set
            {
                if (_currentPage == null || !_currentPage.Equals(value))
                {
                    _currentPage = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(IsSettingsPageActive));
                    RaisePropertyChanged(nameof(IsOperatorPageActive));
                    RaisePropertyChanged(nameof(ShowNewVersionButton));
                    RaisePropertyChanged(nameof(ShowDragAndDropHint));
                }
            }
        }

        public bool IsMediaListLoading
        {
            get => _isMediaListLoading;
            set
            {
                if (_isMediaListLoading != value)
                {
                    _isMediaListLoading = value;
                    RaisePropertyChanged();
                }
            }
        }

        private void OnMediaListUpdating(MediaListUpdatingMessage message)
        {
            IsMediaListLoading = true;
        }

        private void OnMediaListUpdated(MediaListUpdatedMessage message)
        {
            IsMediaListLoading = false;
            IsMediaListEmpty = message.Count == 0;
        }

        private void HandleMediaWindowVisibilityChangedEvent(object sender, WindowVisibilityChangedEventArgs e)
        {
            if (e.Visible)
            {
                Application.Current.MainWindow?.Activate();
            }

            RaisePropertyChanged(nameof(AlwaysOnTop));
        }

        private void HandleAlwaysOnTopChangedEvent(object sender, EventArgs e)
        {
            RaisePropertyChanged(nameof(AlwaysOnTop));
        }

        private void HandlePageNavigationEvent(object sender, NavigationEventArgs e)
        {
            _currentPageName = e.PageName;
            CurrentPage = _pageService.GetPage(e.PageName);

            RaisePropertyChanged(nameof(IsUnhideButtonVisible));
        }

        private void HandleMediaMonitorChangedEvent(object sender, MonitorChangedEventArgs e)
        {
            RaisePropertyChanged(nameof(AlwaysOnTop));
        }
        
        private void SanityChecks()
        {
            // checks are performed in order of importance and subsequent
            // checks only made if previous ones pass.
            Task.Delay(2000).ContinueWith(_ => CheckControlledFolderAccess() &&
                                               CheckVersionData() && CheckLogLevel());
        }

        private bool CheckControlledFolderAccess()
        {
            // Windows 10 Controlled folder access may be enabled preventing
            // OnlyM from writing to its database.
            var databaseFolder = FileUtils.GetOnlyMDatabaseFolder();

            var tempFileName = Guid.NewGuid().ToString("N");
            var fullPath = Path.Combine(databaseFolder, tempFileName);

            try
            {
                File.Create(fullPath).Close();
                File.Delete(fullPath);

                return true;
            }
            catch (UnauthorizedAccessException)
            {
                Log.Logger.Warning("OnlyM cannot write to its database folder. Perhaps controlled folder access is enabled");
                _snackbarService.EnqueueWithOk(Properties.Resources.ALLOW_DB_ACCESS, Properties.Resources.OK);
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Checking controlled folder access");
            }

            return false;
        }
        
        private bool CheckLogLevel()
        {
            switch (_optionsService.LogEventLevel)
            {
                case LogEventLevel.Debug:
                case LogEventLevel.Verbose:
                    _snackbarService.EnqueueWithOk(Properties.Resources.LOGGING_LEVEL_HIGH, Properties.Resources.OK);
                    return false;
            }

            return true;
        }

        private bool CheckVersionData()
        {
            var latestVersion = VersionDetection.GetLatestReleaseVersion();
            if (latestVersion != null && latestVersion > VersionDetection.GetCurrentVersion())
            {
                // there is a new version....
                _newVersionAvailable = true;
                RaisePropertyChanged(nameof(ShowNewVersionButton));

                _snackbarService.Enqueue(
                    Properties.Resources.NEW_UPDATE_AVAILABLE, 
                    Properties.Resources.VIEW, 
                    LaunchReleasePage);

                return false;
            }

            return true;
        }

        private void InitCommands()
        {
            GotoSettingsCommand = new RelayCommand(NavigateSettings);
            GotoOperatorCommand = new RelayCommand(NavigateOperator);
            LaunchMediaFolderCommand = new RelayCommand(LaunchMediaFolder);
            LaunchHelpPageCommand = new RelayCommand(LaunchHelpPage);
            LaunchReleasePageCommand = new RelayCommand(LaunchReleasePage);
            UnhideCommand = new RelayCommand(UnhideAll);
        }

        private void UnhideAll()
        {
            _hiddenMediaItemsService.UnhideAllMediaItems();
        }

        private void LaunchReleasePage()
        {
            Process.Start(VersionDetection.LatestReleaseUrl);
        }

        private void LaunchHelpPage()
        {
            Process.Start(@"https://github.com/AntonyCorbett/OnlyM/wiki");
        }

        private void LaunchMediaFolder()
        {
            if (Directory.Exists(_optionsService.MediaFolder))
            {
                Process.Start(_optionsService.MediaFolder);
            }
        }

        private void NavigateOperator()
        {
            _optionsService.Save();
            _pageService.GotoOperatorPage();
        }

        private void NavigateSettings()
        {
            // prevent navigation to the settings page when media is in flux...
            if (!_mediaStatusChangingService.IsMediaStatusChanging())
            {
                _pageService.GotoSettingsPage();
            }
        }

        private bool ForceSoftwareRendering()
        {
            // https://blogs.msdn.microsoft.com/jgoldb/2010/06/22/software-rendering-usage-in-wpf/
            // renderingTier values:
            // 0 => No graphics hardware acceleration available for the application on the device
            //      and DirectX version level is less than version 7.0
            // 1 => Partial graphics hardware acceleration available on the video card. This 
            //      corresponds to a DirectX version that is greater than or equal to 7.0 and 
            //      less than 9.0.
            // 2 => A rendering tier value of 2 means that most of the graphics features of WPF 
            //      should use hardware acceleration provided the necessary system resources have 
            //      not been exhausted. This corresponds to a DirectX version that is greater 
            //      than or equal to 9.0.
            int renderingTier = RenderCapability.Tier >> 16;
            return renderingTier == 0;
        }

        private void HandleCopyingFilesProgressEvent(object sender, FilesCopyProgressEventArgs e)
        {
            IsBusy = e.Status == FileCopyStatus.StartingCopy;
        }

        private void HandleHiddenItemsChangedEvent(object sender, EventArgs e)
        {
            RaisePropertyChanged(nameof(IsUnhideButtonVisible));
        }
    }
}