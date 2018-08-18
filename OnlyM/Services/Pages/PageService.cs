namespace OnlyM.Services.Pages
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Forms;
    
    using Core.Models;
    using Core.Services.Monitors;
    using Core.Services.Options;

    using GalaSoft.MvvmLight.Messaging;

    using MediaChanging;
    using MediaElementAdaption;
    using Models;
    using PubSubMessages;
    using Serilog;
    using Snackbar;
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
        private readonly IActiveMediaItemsService _activeMediaItemsService;
        private readonly (int dpiX, int dpiY) _systemDpi;

        private MediaWindow _mediaWindow;
        private double _operatorPageScrollerPosition;
        private double _settingsPageScrollerPosition;
        
        public event EventHandler MediaMonitorChangedEvent;

        public event EventHandler<NavigationEventArgs> NavigationEvent;

        public event EventHandler<MediaEventArgs> MediaChangeEvent;

        public event EventHandler<PositionChangedEventArgs> MediaPositionChangedEvent;

        public event EventHandler MediaWindowOpenedEvent;

        public event EventHandler MediaWindowClosedEvent;

        public event EventHandler<WindowVisibilityChangedEventArgs> MediaWindowVisibilityChanged;
        
        public event EventHandler<MediaNearEndEventArgs> MediaNearEndEvent;

        public PageService(
            IMonitorsService monitorsService,
            IOptionsService optionsService,
            IActiveMediaItemsService activeMediaItemsService,
            ISnackbarService snackbarService)
        {
            _monitorsService = monitorsService;
            _optionsService = optionsService;
            _snackbarService = snackbarService;
            _activeMediaItemsService = activeMediaItemsService;

            _optionsService.ImageFadeTypeChangedEvent += HandleImageFadeTypeChangedEvent;
            _optionsService.ImageFadeSpeedChangedEvent += HandleImageFadeSpeedChangedEvent;
            _optionsService.MediaMonitorChangedEvent += HandleMediaMonitorChangedEvent;
            _optionsService.PermanentBackdropChangedEvent += HandlePermanentBackdropChangedEvent;
            _optionsService.RenderingMethodChangedEvent += HandleRenderingMethodChangedEvent;

            _systemDpi = WindowPlacement.GetDpiSettings();

            Messenger.Default.Register<ShutDownMessage>(this, OnShutDown);
        }

        public bool ApplicationIsClosing { get; private set; }

        public ScrollViewer ScrollViewer { get; set; }

        public string OperatorPageName => "OperatorPage";

        public string SettingsPageName => "SettingsPage";

        public void GotoOperatorPage()
        {
            _settingsPageScrollerPosition = ScrollViewer?.VerticalOffset ?? 0.0;
            OnNavigationEvent(new NavigationEventArgs { PageName = OperatorPageName });
        }

        public void GotoSettingsPage()
        {
            var _ = _settingsPage.Value;   // ensure created otherwise doesn't receive navigation event
            _operatorPageScrollerPosition = ScrollViewer.VerticalOffset;
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
        
        public void OpenMediaWindow(bool requiresVisibleWindow)
        {
            try
            {
                EnsureMediaWindowCreated();

                if (requiresVisibleWindow)
                {
                    var targetMonitor = _monitorsService.GetSystemMonitor(_optionsService.Options.MediaMonitorId);
                    if (targetMonitor != null)
                    {
                        Log.Logger.Information("Opening media window");

                        LocateWindowAtOrigin(_mediaWindow, targetMonitor.Monitor);

                        _mediaWindow.Topmost = true;

                        _mediaWindow.Show();

                        MediaWindowOpenedEvent?.Invoke(this, EventArgs.Empty);
                    }
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

        public async Task StartMedia(
            MediaItem mediaItemToStart, 
            IReadOnlyCollection<MediaItem> currentMediaItems, 
            bool startFromPaused)
        {
            try
            {
                bool requiresVisibleWindow = mediaItemToStart.MediaType.Classification != MediaClassification.Audio;
                OpenMediaWindow(requiresVisibleWindow);

                await _mediaWindow.StartMedia(
                    mediaItemToStart,
                    currentMediaItems,
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

        public bool IsMediaWindowVisible => _mediaWindow != null && 
                                            _mediaWindow.IsVisible && 
                                            _mediaWindow.Visibility == Visibility.Visible;

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
            SetScrollerPosition(e.PageName);
        }

        private void SetScrollerPosition(string pageName)
        {
            if (pageName.Equals(OperatorPageName))
            {
                ScrollViewer?.ScrollToVerticalOffset(_operatorPageScrollerPosition);
            }
            else if (pageName.Equals(SettingsPageName))
            {
                ScrollViewer?.ScrollToVerticalOffset(_settingsPageScrollerPosition);
            }
        }

        private void OnShutDown(ShutDownMessage message)
        {
            ApplicationIsClosing = true;
            CloseMediaWindow();
        }
        
        private void RelocateMediaWindow(string originalMonitorId)
        {
            if (_mediaWindow != null)
            {
                var isVisible = _mediaWindow.IsVisible;

                var targetMonitor = _monitorsService.GetSystemMonitor(_optionsService.Options.MediaMonitorId);
                if (targetMonitor != null)
                {
                    _mediaWindow.Hide();
                    _mediaWindow.WindowState = WindowState.Normal;

                    LocateWindowAtOrigin(_mediaWindow, targetMonitor.Monitor);

                    _mediaWindow.Topmost = true;

                    _mediaWindow.WindowState = WindowState.Maximized;

                    _mediaWindow.Show();

                    if (!isVisible)
                    {
                        _mediaWindow.Hide();
                    }
                }
            }
            else if (_optionsService.Options.PermanentBackdrop)
            {
                OpenMediaWindow(requiresVisibleWindow: true);
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

            SubscribeMediaWindowEvents();
        }

        private void SubscribeMediaWindowEvents()
        {
            _mediaWindow.MediaChangeEvent += HandleMediaChangeEvent;
            _mediaWindow.MediaPositionChangedEvent += HandleMediaPositionChangedEvent;
            _mediaWindow.MediaNearEndEvent += HandleMediaNearEndEvent;
            _mediaWindow.IsVisibleChanged += HandleMediaWindowVisibility;
            _mediaWindow.Loaded += HandleLoaded;
        }

        private void HandleMediaWindowVisibility(object sender, DependencyPropertyChangedEventArgs e)
        {
            MediaWindowVisibilityChanged?.Invoke(this, new WindowVisibilityChangedEventArgs { Visible = _mediaWindow.Visibility == Visibility.Visible });
        }

        private void UnsubscribeMediaWindowEvents()
        {
            _mediaWindow.MediaChangeEvent -= HandleMediaChangeEvent;
            _mediaWindow.MediaPositionChangedEvent -= HandleMediaPositionChangedEvent;
            _mediaWindow.MediaNearEndEvent -= HandleMediaNearEndEvent;
            _mediaWindow.IsVisibleChanged -= HandleMediaWindowVisibility;
            _mediaWindow.Loaded -= HandleLoaded;
        }

        private void BringJwlToFront()
        {
            if (_optionsService.Options.JwLibraryCompatibilityMode)
            {
                JwLibHelper.BringToFront();
                Thread.Sleep(100);
            }
        }

        private void HandleLoaded(object sender, RoutedEventArgs e)
        {
            ((Window)sender).WindowState = WindowState.Maximized;
        }

        private void HandleMediaPositionChangedEvent(object sender, PositionChangedEventArgs e)
        {
            MediaPositionChangedEvent?.Invoke(this, e);
        }

        private void HandleMediaNearEndEvent(object sender, MediaNearEndEventArgs e)
        {
            MediaNearEndEvent?.Invoke(this, e);
        }

        private void HandleMediaChangeEvent(object sender, MediaEventArgs e)
        {
            switch (e.Change)
            {
                case MediaChange.Starting:
                    _activeMediaItemsService.Add(e.MediaItemId, e.Classification);
                    break;

                case MediaChange.Stopped:
                    _activeMediaItemsService.Remove(e.MediaItemId);
                    ManageMediaWindowVisibility();
                    break;
            }

            OnMediaChangeEvent(e);
        }

        private void ManageMediaWindowVisibility()
        {
            if (!_optionsService.Options.PermanentBackdrop && !AnyActiveMediaRequiringVisibleMediaWindow())
            {
                _mediaWindow?.Hide();
                BringJwlToFront();
            }
        }

        private bool AnyActiveMediaRequiringVisibleMediaWindow()
        {
            return _activeMediaItemsService.Any(MediaClassification.Image, MediaClassification.Video);
        }

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

        private void HandleMediaMonitorChangedEvent(object sender, MonitorChangedEventArgs e)
        {
            UpdateMediaMonitor(e.OriginalMonitorId);
        }

        private void OnMediaChangeEvent(MediaEventArgs e)
        {
            MediaChangeEvent?.Invoke(this, e);
        }

        private void UpdateMediaMonitor(string originalMonitorId)
        {
            try
            {
                if (_optionsService.IsMediaMonitorSpecified)
                {
                    RelocateMediaWindow(originalMonitorId);
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
                OpenMediaWindow(requiresVisibleWindow: true);
            }
            else if (!_activeMediaItemsService.Any())
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

        private void CloseMediaWindow()
        {
            if (_mediaWindow != null)
            {
                AllowMediaWindowToClose = true;

                BringJwlToFront();

                UnsubscribeMediaWindowEvents();

                _mediaWindow.Close();
                _mediaWindow = null;

                MediaWindowClosedEvent?.Invoke(this, EventArgs.Empty);
            }
        }

        private void HandleRenderingMethodChangedEvent(object sender, EventArgs e)
        {
            _mediaWindow?.UpdateRenderingMethod();
        }
    }
}
