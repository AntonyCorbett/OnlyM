using OnlyM.Services.Snackbar;

namespace OnlyM.Services.Pages
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Forms;
    using Core.Services.Monitors;
    using Core.Services.Options;
    using GalaSoft.MvvmLight.Messaging;
    using Models;
    using PubSubMessages;
    using Serilog;
    using Unosquare.FFME.Events;
    using Windows;
    using WindowsPositioning;

    // ReSharper disable once ClassNeverInstantiated.Global
    internal sealed class PageService : IPageService
    {
        private readonly Lazy<OperatorPage> _operatorPage = new Lazy<OperatorPage>(() => new OperatorPage());
        private readonly Lazy<SettingsPage> _settingsPage = new Lazy<SettingsPage>(() => new SettingsPage());
        
        private readonly IMonitorsService _monitorsService;
        private readonly IOptionsService _optionsService;
        private readonly ISnackbarService _snackbarService;
        private readonly (int dpiX, int dpiY) _systemDpi;

        private MediaWindow _mediaWindow;

        public event EventHandler MediaMonitorChangedEvent;

        public event EventHandler<NavigationEventArgs> NavigationEvent;

        public event EventHandler<MediaEventArgs> MediaChangeEvent;

        public event EventHandler<PositionChangedRoutedEventArgs> MediaPositionChangedEvent;

        public event EventHandler MediaWindowOpenedEvent;

        public event EventHandler MediaWindowClosedEvent;

        public PageService(
            IMonitorsService monitorsService,
            IOptionsService optionsService,
            ISnackbarService snackbarService)
        {
            _monitorsService = monitorsService;
            _optionsService = optionsService;
            _snackbarService = snackbarService;

            _optionsService.ImageFadeTypeChangedEvent += HandleImageFadeTypeChangedEvent;
            _optionsService.ImageFadeSpeedChangedEvent += HandleImageFadeSpeedChangedEvent;
            _optionsService.MediaMonitorChangedEvent += HandleMediaMonitorChangedEvent;
            _optionsService.PermanentBackdropChangedEvent += HandlePermanentBackdropChangedEvent;
            
            _systemDpi = WindowPlacement.GetDpiSettings();

            Messenger.Default.Register<ShutDownMessage>(this, OnShutDown);
        }

        public bool ApplicationIsClosing { get; set; }
        
        public string OperatorPageName => "OperatorPage";

        public string SettingsPageName => "SettingsPage";

        public void GotoOperatorPage()
        {
            OnNavigationEvent(new NavigationEventArgs { PageName = OperatorPageName });
        }

        public void GotoSettingsPage()
        {
            OnNavigationEvent(new NavigationEventArgs { PageName = SettingsPageName });
        }

        public FrameworkElement GetPage(string pageName)
        {
            if (pageName.Equals(OperatorPageName))
            {
                return _operatorPage.Value;
            }

            if (pageName.Equals(SettingsPageName))
            {
                return _settingsPage.Value;
            }

            throw new ArgumentOutOfRangeException(nameof(pageName));
        }
        
        public void OpenMediaWindow()
        {
            Log.Logger.Information("Opening media window");

            try
            {
                EnsureMediaWindowCreated();

                var targetMonitor = _monitorsService.GetSystemMonitor(_optionsService.Options.MediaMonitorId);
                if (targetMonitor != null)
                {
                    LocateWindowAtOrigin(_mediaWindow, targetMonitor.Monitor);
                    
                    _mediaWindow.Topmost = true;

                    _mediaWindow.Show();
                    
                    // ensure it shows over topmost windows of other applications.
                    _mediaWindow.Activate();
                    
                    OnMediaWindowOpened();
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Could not open media window");
            }
        }

        public bool AllowMediaWindowToClose { get; set; }

        public void CacheImageItem(MediaItem mediaItem)
        {
            if (_mediaWindow != null && mediaItem != null)
            {
                _mediaWindow.CacheImageItem(mediaItem);
            }
        }

        public async Task StartMedia(MediaItem mediaItemToStart, MediaItem currentMediaItem, bool startFromPaused)
        {
            try
            {
                OpenMediaWindow();
                
                await _mediaWindow.StartMedia(
                    mediaItemToStart, 
                    currentMediaItem, 
                    startFromPaused);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Could not start media: {mediaItemToStart.FilePath}");
            }
        }

        public async Task StopMediaAsync(MediaItem mediaItem)
        {
            if (_mediaWindow != null)
            {
                await _mediaWindow.StopMediaAsync(mediaItem);
            }
        }

        public async Task PauseMediaAsync(MediaItem mediaItem)
        {
            if (_mediaWindow != null)
            {
                await _mediaWindow.PauseMediaAsync(mediaItem);
            }
        }

        public bool IsMediaWindowVisible => _mediaWindow != null && _mediaWindow.IsVisible;

        private void LocateWindowAtOrigin(Window window, Screen monitor)
        {
            var area = monitor.WorkingArea;

            var left = (area.Left * 96) / _systemDpi.dpiX;
            var top = (area.Top * 96) / _systemDpi.dpiY;

            Log.Logger.Verbose($"Monitor = {monitor.DeviceName} Left = {left}, top = {top}");

            // these seemingly redundant sizing statements are required!
            window.Left = 0;
            window.Top = 0;
            window.Width = 0;
            window.Height = 0;

            window.Left = left;
            window.Top = top;
        }

        private void OnNavigationEvent(NavigationEventArgs e)
        {
            NavigationEvent?.Invoke(this, e);
        }

        private void OnShutDown(ShutDownMessage message)
        {
            ApplicationIsClosing = true;
            CloseMediaWindow();
        }
        
        private void RelocateMediaWindow()
        {
            if (_mediaWindow != null)
            {
                var targetMonitor = _monitorsService.GetSystemMonitor(_optionsService.Options.MediaMonitorId);
                if (targetMonitor != null)
                {
                    _mediaWindow.Hide();
                    _mediaWindow.WindowState = WindowState.Normal;

                    LocateWindowAtOrigin(_mediaWindow, targetMonitor.Monitor);

                    _mediaWindow.Topmost = true;
                    _mediaWindow.WindowState = WindowState.Maximized;
                    _mediaWindow.Show();
                }
            }
            else if (_optionsService.Options.PermanentBackdrop)
            {
                OpenMediaWindow();
            }
        }

        private void OnMediaMonitorChangedEvent()
        {
            MediaMonitorChangedEvent?.Invoke(this, EventArgs.Empty);
        }

        private void CreateMediaWindow()
        {
            AllowMediaWindowToClose = false;

            _mediaWindow = new MediaWindow(_optionsService, _snackbarService)
            {
                ImageFadeType = _optionsService.Options.ImageFadeType,
                ImageFadeSpeed = _optionsService.Options.ImageFadeSpeed
            };

            _mediaWindow.MediaChangeEvent += HandleMediaChangeEvent;
            _mediaWindow.MediaPositionChangedEvent += HandleMediaPositionChangedEvent;
            _mediaWindow.FinishedWithWindowEvent += HandleFinishedWithWindowEvent;
            _mediaWindow.Loaded += HandleLoaded;
        }

        private void HandleFinishedWithWindowEvent(object sender, EventArgs e)
        {
            if (!_optionsService.Options.PermanentBackdrop)
            {
                CloseMediaWindow();
            }
        }

        private void HandleLoaded(object sender, RoutedEventArgs e)
        {
            ((Window)sender).WindowState = WindowState.Maximized;
        }

        private void HandleMediaPositionChangedEvent(object sender, PositionChangedRoutedEventArgs e)
        {
            MediaPositionChangedEvent?.Invoke(this, e);
        }

        private void HandleMediaChangeEvent(object sender, MediaEventArgs e)
        {
            switch (e.Change)
            {
                case MediaChange.Started:
                    CurrentMediaId = e.MediaItemId;
                    break;

                case MediaChange.Stopped:
                    if (e.MediaItemId == CurrentMediaId)
                    {
                        CurrentMediaId = Guid.Empty;
                    }

                    break;
            }

            OnMediaChangeEvent(e);
        }

        public Guid CurrentMediaId { get; private set; }

        public bool IsMediaItemActive => CurrentMediaId != Guid.Empty;

        private void HandleImageFadeTypeChangedEvent(object sender, EventArgs e)
        {
            if (_mediaWindow != null)
            {
                _mediaWindow.ImageFadeType = _optionsService.Options.ImageFadeType;
            }
        }

        private void HandleImageFadeSpeedChangedEvent(object sender, EventArgs e)
        {
            if (_mediaWindow != null)
            {
                _mediaWindow.ImageFadeSpeed = _optionsService.Options.ImageFadeSpeed;
            }
        }

        private void HandleMediaMonitorChangedEvent(object sender, EventArgs e)
        {
            UpdateMediaMonitor();
        }

        private void OnMediaChangeEvent(MediaEventArgs e)
        {
            MediaChangeEvent?.Invoke(this, e);
        }

        private void UpdateMediaMonitor()
        {
            try
            {
                if (_optionsService.IsMediaMonitorSpecified)
                {
                    RelocateMediaWindow();
                }
                else
                {
                    // media monitor = "None"
                    CloseMediaWindow();
                }

                OnMediaMonitorChangedEvent();
                System.Windows.Application.Current.MainWindow?.Activate();
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Could not change monitor");
            }
        }

        private void HandlePermanentBackdropChangedEvent(object sender, EventArgs e)
        {
            if (_optionsService.Options.PermanentBackdrop)
            {
                OpenMediaWindow();
            }
            else
            {
                CloseMediaWindow();
            }
        }

        private void EnsureMediaWindowCreated()
        {
            if (_mediaWindow == null)
            {
                CreateMediaWindow();
            }
        }

        private void OnMediaWindowOpened()
        {
            MediaWindowOpenedEvent?.Invoke(this, EventArgs.Empty);
        }

        private void OnMediaWindowClosed()
        {
            MediaWindowClosedEvent?.Invoke(this, EventArgs.Empty);
        }

        private void CloseMediaWindow()
        {
            if (_mediaWindow != null)
            {
                AllowMediaWindowToClose = true;

                if (_optionsService.Options.JwLibraryCompatibilityMode)
                {
                    JwLibHelper.BringToFront();
                    Thread.Sleep(100);
                }

                _mediaWindow.MediaChangeEvent -= HandleMediaChangeEvent;
                _mediaWindow.MediaPositionChangedEvent -= HandleMediaPositionChangedEvent;
                _mediaWindow.FinishedWithWindowEvent -= HandleFinishedWithWindowEvent;
                _mediaWindow.Loaded -= HandleLoaded;

                _mediaWindow.Close();
                _mediaWindow = null;

                OnMediaWindowClosed();
            }
        }
    }
}
