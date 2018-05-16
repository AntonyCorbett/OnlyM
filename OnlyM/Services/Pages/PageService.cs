namespace OnlyM.Services.Pages
{
    using System;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Forms;
    using Core.Models;
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
        private readonly Lazy<MediaWindow> _mediaWindow;

        private readonly IMonitorsService _monitorsService;
        private readonly IOptionsService _optionsService;
        private readonly (int dpiX, int dpiY) _systemDpi;
        
        public event EventHandler MediaMonitorChangedEvent;

        public event EventHandler<NavigationEventArgs> NavigationEvent;

        public event EventHandler<MediaEventArgs> MediaChangeEvent;

        public event EventHandler<PositionChangedRoutedEventArgs> MediaPositionChangedEvent;

        public PageService(
            IMonitorsService monitorsService,
            IOptionsService optionsService)
        {
            _monitorsService = monitorsService;
            _optionsService = optionsService;

            _mediaWindow = new Lazy<MediaWindow>(MediaWindowCreation);

            _optionsService.ImageFadeTypeChangedEvent += HandleImageFadeTypeChangedEvent;
            _optionsService.ImageFadeSpeedChangedEvent += HandleImageFadeSpeedChangedEvent;
            _optionsService.MediaMonitorChangedEvent += HandleMediaMonitorChangedEvent;

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
            try
            {
                var targetMonitor = _monitorsService.GetSystemMonitor(_optionsService.Options.MediaMonitorId);
                if (targetMonitor != null)
                {
                    ShowWindowFullScreenOnTop(_mediaWindow.Value, targetMonitor);
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Could not open media window");
            }
        }

        public void CloseMediaWindow()
        {
            if (_mediaWindow.IsValueCreated)
            {
                _mediaWindow.Value.Close();
            }
        }

        public void CacheImageItem(MediaItem mediaItem)
        {
            if (_mediaWindow.IsValueCreated && mediaItem != null)
            {
                _mediaWindow.Value.CacheImageItem(mediaItem);
            }
        }

        public void StartMedia(MediaItem mediaItemToStart, MediaItem currentMediaItem)
        {
            try
            {
                OpenMediaWindow();
                
                _mediaWindow.Value.StartMedia(
                    mediaItemToStart, 
                    currentMediaItem, 
                    _optionsService.Options.ShowVideoSubtitles);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Could not start media: {mediaItemToStart.FilePath}");
            }
        }

        public async Task StopMediaAsync(MediaItem mediaItem)
        {
            if (_mediaWindow.IsValueCreated)
            {
                await _mediaWindow.Value.StopMediaAsync(mediaItem);
            }
        }
        
        public bool IsMediaWindowVisible => _mediaWindow.IsValueCreated && _mediaWindow.Value.IsVisible;

        private void ShowWindowFullScreenOnTop(Window window, SystemMonitor monitor)
        {
            LocateWindowAtOrigin(window, monitor.Monitor);

            window.Topmost = true;
            window.Show();
        }

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
        
        private void HideMediaWindow()
        {
            if (_mediaWindow.IsValueCreated)
            {
                _mediaWindow.Value.Hide();
            }
        }

        private void RelocateMediaWindow()
        {
            if (_mediaWindow.IsValueCreated)
            {
                var targetMonitor = _monitorsService.GetSystemMonitor(_optionsService.Options.MediaMonitorId);
                if (targetMonitor != null)
                {
                    _mediaWindow.Value.Hide();
                    _mediaWindow.Value.WindowState = WindowState.Normal;

                    LocateWindowAtOrigin(_mediaWindow.Value, targetMonitor.Monitor);

                    _mediaWindow.Value.Topmost = true;
                    _mediaWindow.Value.WindowState = WindowState.Maximized;
                    _mediaWindow.Value.Show();
                }
            }
            else
            {
                OpenMediaWindow();
            }
        }

        private void OnMediaMonitorChangedEvent()
        {
            MediaMonitorChangedEvent?.Invoke(this, EventArgs.Empty);
        }

        private MediaWindow MediaWindowCreation()
        {
            var result = new MediaWindow(_optionsService)
            {
                ImageFadeType = _optionsService.Options.ImageFadeType,
                ImageFadeSpeed = _optionsService.Options.ImageFadeSpeed
            };

            result.MediaChangeEvent += HandleMediaChangeEvent;
            result.MediaPositionChangedEvent += HandleMediaPositionChangedEvent;
            result.Loaded += HandleLoaded;

            return result;
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
            if (_mediaWindow.IsValueCreated)
            {
                _mediaWindow.Value.ImageFadeType = _optionsService.Options.ImageFadeType;
            }
        }

        private void HandleImageFadeSpeedChangedEvent(object sender, EventArgs e)
        {
            if (_mediaWindow.IsValueCreated)
            {
                _mediaWindow.Value.ImageFadeSpeed = _optionsService.Options.ImageFadeSpeed;
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
                    HideMediaWindow();
                }

                OnMediaMonitorChangedEvent();
                System.Windows.Application.Current.MainWindow?.Activate();
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Could not change monitor");
            }
        }
    }
}
