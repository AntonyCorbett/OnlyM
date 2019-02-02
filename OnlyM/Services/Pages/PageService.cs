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
    using GalaSoft.MvvmLight.Threading;
    using MediaChanging;
    using MediaElementAdaption;
    using Models;
    using OnlyM.Core.Services.CommandLine;
    using OnlyM.Core.Services.Database;
    using OnlyM.CoreSys.Services.Snackbar;
    using OnlyM.CoreSys.WindowsPositioning;
    using OnlyM.Services.WebBrowser;
    using PubSubMessages;
    using Serilog;
    using Windows;
    
    // ReSharper disable once ClassNeverInstantiated.Global
    internal sealed class PageService : IPageService
    {
        private readonly Lazy<OperatorPage> _operatorPage = new Lazy<OperatorPage>(() => new OperatorPage());
        private readonly Lazy<SettingsPage> _settingsPage = new Lazy<SettingsPage>(() => new SettingsPage());
        
        private readonly IMonitorsService _monitorsService;
        private readonly IOptionsService _optionsService;
        private readonly ISnackbarService _snackbarService;
        private readonly IDatabaseService _databaseService;
        private readonly IActiveMediaItemsService _activeMediaItemsService;
        private readonly ICommandLineService _commandLineService;
        private readonly (int dpiX, int dpiY) _systemDpi;
        
        private MediaWindow _mediaWindow;
        private double _operatorPageScrollerPosition;
        private double _settingsPageScrollerPosition;
        
        public PageService(
            IMonitorsService monitorsService,
            IOptionsService optionsService,
            IActiveMediaItemsService activeMediaItemsService,
            ISnackbarService snackbarService,
            IDatabaseService databaseService,
            ICommandLineService commandLineService)
        {
            _monitorsService = monitorsService;
            _optionsService = optionsService;
            _snackbarService = snackbarService;
            _databaseService = databaseService;
            _activeMediaItemsService = activeMediaItemsService;
            _commandLineService = commandLineService;

            _optionsService.MediaMonitorChangedEvent += HandleMediaMonitorChangedEvent;
            _optionsService.PermanentBackdropChangedEvent += HandlePermanentBackdropChangedEvent;
            _optionsService.RenderingMethodChangedEvent += HandleRenderingMethodChangedEvent;

            _systemDpi = WindowsPlacement.GetDpiSettings();
            
            Messenger.Default.Register<ShutDownMessage>(this, OnShutDown);
            Messenger.Default.Register<MirrorWindowMessage>(this, OnMirrorWindowMessage);
        }

        public event EventHandler MediaMonitorChangedEvent;

        public event EventHandler<NavigationEventArgs> NavigationEvent;

        public event EventHandler<MediaEventArgs> MediaChangeEvent;

        public event EventHandler<SlideTransitionEventArgs> SlideTransitionEvent;

        public event EventHandler<PositionChangedEventArgs> MediaPositionChangedEvent;

        public event EventHandler MediaWindowOpenedEvent;

        public event EventHandler MediaWindowClosedEvent;

        public event EventHandler<WindowVisibilityChangedEventArgs> MediaWindowVisibilityChanged;

        public event EventHandler<MediaNearEndEventArgs> MediaNearEndEvent;

        public event EventHandler<WebBrowserProgressEventArgs> WebStatusEvent;

        public bool ApplicationIsClosing { get; private set; }

        public ScrollViewer ScrollViewer { get; set; }

        public string OperatorPageName => "OperatorPage";

        public string SettingsPageName => "SettingsPage";

        public bool AllowMediaWindowToClose { get; set; }

        public bool IsMediaWindowVisible => _mediaWindow != null &&
                                            _mediaWindow.IsVisible &&
                                            _mediaWindow.Visibility == Visibility.Visible;

        public void InitMediaWindow()
        {
            // used to instantiate the media window and its controls (and then
            // possibly hide it immediately). Required to correctly configure
            // the CefSharp browser control.
            OpenMediaWindow(requiresVisibleWindow: true, isVideo: false);
            ManageMediaWindowVisibility();
        }

        public void GotoOperatorPage()
        {
            _settingsPageScrollerPosition = ScrollViewer?.VerticalOffset ?? 0.0;
            OnNavigationEvent(new NavigationEventArgs { PageName = OperatorPageName });
        }

        public void GotoSettingsPage()
        {
#pragma warning disable SA1312 // Variable names must begin with lower-case letter
            var _ = _settingsPage.Value;   // ensure created otherwise doesn't receive navigation event
#pragma warning restore SA1312 // Variable names must begin with lower-case letter

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
        
        public void CacheImageItem(MediaItem mediaItem)
        {
            if (_mediaWindow != null && mediaItem != null)
            {
                _mediaWindow.CacheImageItem(mediaItem);
            }
        }

        public int GotoPreviousSlide()
        {
            return _mediaWindow.GotoPreviousSlide();
        }

        public int GotoNextSlide()
        {
            return _mediaWindow.GotoNextSlide();
        }

        public async Task StartMedia(
            MediaItem mediaItemToStart, 
            IReadOnlyCollection<MediaItem> currentMediaItems, 
            bool startFromPaused)
        {
            try
            {
                bool requiresVisibleWindow = mediaItemToStart.MediaType.Classification != MediaClassification.Audio;
                OpenMediaWindow(requiresVisibleWindow, mediaItemToStart.IsVideo);

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
                // close any mirror...
                Messenger.Default.Send(new MirrorWindowMessage { MediaItemId = mediaItem.Id, UseMirror = false });

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

        public async void ForciblyStopAllPlayback(IReadOnlyCollection<MediaItem> activeItems)
        {
            foreach (var item in activeItems)
            {
                await _mediaWindow.StopMediaAsync(item, true);
            }
        }

        private void PositionMediaWindow(Window window, Screen monitor, bool isVideo)
        {
            MediaWindowPositionHelper.PositionMediaWindow(
                _optionsService, _commandLineService, window, monitor, _systemDpi, isVideo);
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
        
        private void RelocateMediaWindow()
        {
            if (_mediaWindow != null)
            {
                var isVisible = _mediaWindow.IsVisible;

                var targetMonitor = _monitorsService.GetSystemMonitor(_optionsService.MediaMonitorId);
                if (targetMonitor != null)
                {
                    _mediaWindow.Hide();
                    _mediaWindow.WindowState = WindowState.Normal;

                    PositionMediaWindow(_mediaWindow, targetMonitor.Monitor, false);

                    _mediaWindow.Topmost = true;

                    _mediaWindow.Show();
                    
                    if (!isVisible)
                    {
                        _mediaWindow.Hide();
                    }
                }
            }
            else if (_optionsService.PermanentBackdrop)
            {
                OpenMediaWindow(requiresVisibleWindow: true, isVideo: false);
            }
        }
        
        private void CreateMediaWindow()
        {
            AllowMediaWindowToClose = false;

            _mediaWindow = new MediaWindow(_optionsService, _snackbarService, _databaseService, _monitorsService);

            SubscribeMediaWindowEvents();
        }

        private void SubscribeMediaWindowEvents()
        {
            _mediaWindow.MediaChangeEvent += HandleMediaChangeEvent;
            _mediaWindow.SlideTransitionEvent += HandleSlideTransitionEvent;
            _mediaWindow.MediaPositionChangedEvent += HandleMediaPositionChangedEvent;
            _mediaWindow.MediaNearEndEvent += HandleMediaNearEndEvent;
            _mediaWindow.IsVisibleChanged += HandleMediaWindowVisibility;
            _mediaWindow.WebStatusEvent += HandleWebStatusEvent;
        }

        private void HandleWebStatusEvent(object sender, WebBrowserProgressEventArgs e)
        {
            WebStatusEvent?.Invoke(this, e);
        }

        private void HandleMediaWindowVisibility(object sender, DependencyPropertyChangedEventArgs e)
        {
            MediaWindowVisibilityChanged?.Invoke(this, new WindowVisibilityChangedEventArgs { Visible = _mediaWindow.Visibility == Visibility.Visible });
        }

        private void UnsubscribeMediaWindowEvents()
        {
            _mediaWindow.MediaChangeEvent -= HandleMediaChangeEvent;
            _mediaWindow.SlideTransitionEvent -= HandleSlideTransitionEvent;
            _mediaWindow.MediaPositionChangedEvent -= HandleMediaPositionChangedEvent;
            _mediaWindow.MediaNearEndEvent -= HandleMediaNearEndEvent;
            _mediaWindow.IsVisibleChanged -= HandleMediaWindowVisibility;
            _mediaWindow.WebStatusEvent -= HandleWebStatusEvent;
        }

        private void BringJwlToFront()
        {
            if (_optionsService.JwLibraryCompatibilityMode)
            {
                JwLibHelper.BringToFront();
                Thread.Sleep(100);
            }
        }

        private void HandleMediaPositionChangedEvent(object sender, PositionChangedEventArgs e)
        {
            MediaPositionChangedEvent?.Invoke(this, e);
        }

        private void HandleMediaNearEndEvent(object sender, MediaNearEndEventArgs e)
        {
            MediaNearEndEvent?.Invoke(this, e);
        }

        private void HandleSlideTransitionEvent(object sender, SlideTransitionEventArgs e)
        {
            SlideTransitionEvent?.Invoke(this, e);
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

            MediaChangeEvent?.Invoke(this, e);
        }

        private void ManageMediaWindowVisibility()
        {
            if (!_optionsService.PermanentBackdrop && !AnyActiveMediaRequiringVisibleMediaWindow())
            {
                _mediaWindow?.Hide();
                BringJwlToFront();
            }
        }

        private bool AnyActiveMediaRequiringVisibleMediaWindow()
        {
            return _activeMediaItemsService.Any(
                MediaClassification.Image, 
                MediaClassification.Video, 
                MediaClassification.Slideshow,
                MediaClassification.Web);
        }

        private void HandleMediaMonitorChangedEvent(object sender, MonitorChangedEventArgs e)
        {
            UpdateMediaMonitor();
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

                MediaMonitorChangedEvent?.Invoke(this, EventArgs.Empty);
                System.Windows.Application.Current.MainWindow?.Activate();
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Could not change monitor");
            }
        }

        private void HandlePermanentBackdropChangedEvent(object sender, EventArgs e)
        {
            if (_optionsService.PermanentBackdrop)
            {
                OpenMediaWindow(requiresVisibleWindow: true, isVideo: false);
            }
            else if (!_activeMediaItemsService.Any())
            {
                CloseMediaWindow();

                // must use this hack otherwise CefSharp won't display the browser
                // when next required!
                PermanentBackDropHack();
            }
        }

        private void PermanentBackDropHack()
        {
            if (_mediaWindow == null)
            {
                EnsureMediaWindowCreated();
                _mediaWindow.Show();

                Task.Delay(10).ContinueWith(t =>
                {
                    DispatcherHelper.CheckBeginInvokeOnUI(() =>
                    {
                        RelocateMediaWindow();
                        _mediaWindow.Hide();
                    });
                });
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
                _mediaWindow.Dispose();
                _mediaWindow = null;

                MediaWindowClosedEvent?.Invoke(this, EventArgs.Empty);
            }
        }

        private void HandleRenderingMethodChangedEvent(object sender, EventArgs e)
        {
            _mediaWindow?.UpdateRenderingMethod();
        }

        private void OnMirrorWindowMessage(MirrorWindowMessage msg)
        {
            if (_activeMediaItemsService.Exists(msg.MediaItemId))
            {
                _mediaWindow.ShowMirror(msg.UseMirror);
            }
        }

        private void OpenMediaWindow(bool requiresVisibleWindow, bool isVideo)
        {
            try
            {
                EnsureMediaWindowCreated();

                if (requiresVisibleWindow)
                {
                    var targetMonitor = _monitorsService.GetSystemMonitor(_optionsService.MediaMonitorId);
                    if (targetMonitor != null)
                    {
                        Log.Logger.Information("Opening media window");

                        PositionMediaWindow(_mediaWindow, targetMonitor.Monitor, isVideo);

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
    }
}
