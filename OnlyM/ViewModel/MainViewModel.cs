namespace OnlyM.ViewModel
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading.Tasks;
    using System.Windows;
    using AutoUpdates;
    using Core.Services.Options;
    using GalaSoft.MvvmLight;
    using GalaSoft.MvvmLight.Command;
    using GalaSoft.MvvmLight.Messaging;
    using PubSubMessages;
    using Services.Pages;

    // ReSharper disable once ClassNeverInstantiated.Global
    internal class MainViewModel : ViewModelBase
    {
        private readonly IPageService _pageService;
        private readonly IOptionsService _optionsService;

        public MainViewModel(
            IPageService pageService,
            IOptionsService optionsService)
        {
            _pageService = pageService;
            _pageService.NavigationEvent += HandlePageNavigationEvent;
            _pageService.MediaMonitorChangedEvent += HandleMediaMonitorChangedEvent;
            _pageService.MediaWindowOpenedEvent += HandleMediaWindowOpenedEvent;
            _pageService.MediaWindowClosedEvent += HandleMediaWindowClosedEvent;

            _optionsService = optionsService;
            _optionsService.AlwaysOnTopChangedEvent += HandleAlwaysOnTopChangedEvent;

            _pageService.GotoOperatorPage();

            InitCommands();

            Messenger.Default.Register<MediaListUpdatedMessage>(this, OnMediaListUpdated);

            if (!IsInDesignMode && _optionsService.Options.PermanentBackdrop)
            {
                _pageService.OpenMediaWindow();
            }

            GetVersionData();
        }

        private void OnMediaListUpdated(MediaListUpdatedMessage message)
        {
            IsMediaListEmpty = message.Count == 0;
        }

        private void HandleMediaWindowOpenedEvent(object sender, EventArgs e)
        {
            RaisePropertyChanged(nameof(AlwaysOnTop));
        }

        private void HandleMediaWindowClosedEvent(object sender, EventArgs e)
        {
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
        }

        private string _currentPageName;
        private FrameworkElement _currentPage;

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
                }
            }
        }

        private void HandleMediaMonitorChangedEvent(object sender, EventArgs e)
        {
            RaisePropertyChanged(nameof(AlwaysOnTop));
        }

        private void GetVersionData()
        {
            Task.Delay(2000).ContinueWith(_ =>
            {
                var latestVersion = VersionDetection.GetLatestReleaseVersion();
                if (latestVersion != null)
                {
                    if (latestVersion != VersionDetection.GetCurrentVersion())
                    {
                        // there is a new version....
                        _newVersionAvailable = true;
                        RaisePropertyChanged(nameof(ShowNewVersionButton));
                    }
                }
            });
        }


        private bool _newVersionAvailable;

        public bool ShowNewVersionButton => _newVersionAvailable && IsOperatorPageActive;

        public bool AlwaysOnTop => _optionsService.Options.AlwaysOnTop || _pageService.IsMediaWindowVisible;

        public bool IsSettingsPageActive => _currentPageName.Equals(_pageService.SettingsPageName);

        public bool IsOperatorPageActive => _currentPageName.Equals(_pageService.OperatorPageName);

        private bool _isMediaListEmpty;

        public bool IsMediaListEmpty
        {
            get => _isMediaListEmpty;
            set
            {
                if (_isMediaListEmpty != value)
                {
                    _isMediaListEmpty = value;
                    RaisePropertyChanged();
                }
            }
        }

        // commands...
        public RelayCommand GotoSettingsCommand { get; set; }

        public RelayCommand GotoOperatorCommand { get; set; }

        public RelayCommand LaunchMediaFolderCommand { get; set; }

        public RelayCommand LaunchHelpPageCommand { get; set; }

        public RelayCommand LaunchReleasePageCommand { get; set; }

        private void InitCommands()
        {
            GotoSettingsCommand = new RelayCommand(NavigateSettings);
            GotoOperatorCommand = new RelayCommand(NavigateOperator);
            LaunchMediaFolderCommand = new RelayCommand(LaunchMediaFolder);
            LaunchHelpPageCommand = new RelayCommand(LaunchHelpPage);
            LaunchReleasePageCommand = new RelayCommand(LaunchReleasePage);
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
            if (Directory.Exists(_optionsService.Options.MediaFolder))
            {
                Process.Start(_optionsService.Options.MediaFolder);
            }
        }

        private void NavigateOperator()
        {
            _optionsService.Save();
            _pageService.GotoOperatorPage();
        }

        private void NavigateSettings()
        {
            _pageService.GotoSettingsPage();
        }
    }
}