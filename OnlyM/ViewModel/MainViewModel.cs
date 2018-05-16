namespace OnlyM.ViewModel
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Windows;
    using Core.Services.Options;
    using GalaSoft.MvvmLight;
    using GalaSoft.MvvmLight.Command;
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

            _optionsService = optionsService;
            _optionsService.AlwaysOnTopChangedEvent += HandleAlwaysOnTopChangedEvent;

            _pageService.GotoOperatorPage();

            InitCommands();

            if (!IsInDesignMode)
            {
                _pageService.OpenMediaWindow(includeBackdrop: _optionsService.Options.PermanentBackdrop);
            }
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
                }
            }
        }

        private void HandleMediaMonitorChangedEvent(object sender, System.EventArgs e)
        {
            RaisePropertyChanged(nameof(AlwaysOnTop));
        }

        public bool AlwaysOnTop => _optionsService.Options.AlwaysOnTop || _pageService.IsMediaWindowVisible;

        public bool IsSettingsPageActive => _currentPageName.Equals(_pageService.SettingsPageName);

        public bool IsOperatorPageActive => _currentPageName.Equals(_pageService.OperatorPageName);

        public bool IsPlaying => false; // todo: complete

        public bool IsNotPlaying => !IsPlaying;


        // commands...
        public RelayCommand GotoSettingsCommand { get; set; }

        public RelayCommand GotoOperatorCommand { get; set; }

        public RelayCommand LaunchMediaFolderCommand { get; set; }

        private void InitCommands()
        {
            GotoSettingsCommand = new RelayCommand(NavigateSettings, () => IsNotPlaying);
            GotoOperatorCommand = new RelayCommand(NavigateOperator);
            LaunchMediaFolderCommand = new RelayCommand(LaunchMediaFolder);
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