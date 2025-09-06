using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.DependencyInjection;
using OnlyM.Core.Models;
using OnlyM.Core.Services.Database;
using OnlyM.Core.Services.Monitors;
using OnlyM.Core.Services.Options;
using OnlyM.CoreSys.Services.Snackbar;
using OnlyM.CoreSys.WindowsPositioning;
using OnlyM.EventTracking;
using OnlyM.MediaElementAdaption;
using OnlyM.Models;
using OnlyM.Services;
using OnlyM.Services.Pages;
using OnlyM.Services.WebBrowser;
using OnlyM.Services.WebNavHeaderManager;
using OnlyM.ViewModel;
using Serilog;

namespace OnlyM.Windows;

/// <summary>
/// Interaction logic for MediaWindow.xaml
/// </summary>
public sealed partial class MediaWindow : IDisposable
{
    // Zoom settings
    private const double ImageZoomStep = 1.25;
    private const double ImageZoomMin = 1.0;
    private const double ImageZoomMax = 8.0;

    private const int MediaConfirmStopWindowSeconds = 3;

    private readonly ImageDisplayManager _imageDisplayManager;
    private readonly WebDisplayManager _webDisplayManager;
    private readonly AudioManager _audioManager;

    private readonly WebNavHeaderAdmin _webNavHeaderAdmin;

    private readonly IOptionsService _optionsService;
    private readonly ISnackbarService _snackbarService;
    private VideoDisplayManager? _videoDisplayManager;

    private IMediaElement? _videoElement;
    private RenderingMethod _currentRenderingMethod;

    // Zoom settings
    private static readonly Duration ImageZoomDuration = new(TimeSpan.FromMilliseconds(350));
    private readonly CubicEase _imageZoomEase = new() { EasingMode = EasingMode.EaseInOut };

    private double _currentImageZoom = 1.0;

    // Normalized focus point within the bitmap (0..1 in both axes)
    private Point _currentImageFocus = new(0.5, 0.5);
    private bool _isImageZoomed;

    public MediaWindow(
        IOptionsService optionsService,
        ISnackbarService snackbarService,
        IDatabaseService databaseService,
        IMonitorsService monitorsService)
    {
        InitializeComponent();

        _webNavHeaderAdmin = new WebNavHeaderAdmin(WebNavHeader);

        _optionsService = optionsService;
        _snackbarService = snackbarService;

        _imageDisplayManager = new ImageDisplayManager(
            Image1Element, Image2Element, _optionsService);

        _webDisplayManager = new WebDisplayManager(
            Browser, BrowserGrid, databaseService, _optionsService, monitorsService, _snackbarService);

        _audioManager = new AudioManager();

        InitVideoRenderingMethod();

        SubscribeOptionsEvents();
        SubscribeImageEvents();
        SubscribeWebEvents();
        SubscribeAudioEvents();

        // Ensure transforms are present (defensive)
        EnsureImageTransforms(Image1Element);
        EnsureImageTransforms(Image2Element);
    }

    // Routed commands to be used by Window.InputBindings
    public static readonly RoutedUICommand ZoomInImageCommand =
        new("Zoom In Image", nameof(ZoomInImageCommand), typeof(MediaWindow));

    public static readonly RoutedUICommand ZoomOutImageCommand =
        new("Zoom Out Image", nameof(ZoomOutImageCommand), typeof(MediaWindow));

    public static readonly RoutedUICommand ResetImageZoomCommand =
        new("Reset Image Zoom", nameof(ResetImageZoomCommand), typeof(MediaWindow));

    public event EventHandler<MediaEventArgs>? MediaChangeEvent;

    public event EventHandler<SlideTransitionEventArgs>? SlideTransitionEvent;

    public event EventHandler<OnlyMPositionChangedEventArgs>? MediaPositionChangedEvent;

    public event EventHandler<MediaNearEndEventArgs>? MediaNearEndEvent;

    public event EventHandler<WebBrowserProgressEventArgs>? WebStatusEvent;

    public bool IsWindowed { get; set; }

    public void Dispose()
    {
        _videoDisplayManager?.Dispose();
        _audioManager.Dispose();
        VideoElementFfmpeg?.Dispose();
        Browser?.Dispose();
    }

    public void ShowMirror(bool show)
    {
        if (show)
        {
            _ = _webDisplayManager.ShowMirrorAsync();
        }
        else
        {
            _webDisplayManager.CloseMirror();
        }
    }

    public void UpdateRenderingMethod()
    {
        if (_optionsService.RenderingMethod != _currentRenderingMethod)
        {
            InitVideoRenderingMethod();
        }
    }

    public async Task StartMedia(
        MediaItem mediaItemToStart,
        IReadOnlyCollection<MediaItem>? currentMediaItems,
        bool startFromPaused)
    {
        Log.Logger.Information("Starting media {Path}", mediaItemToStart.FilePath);
        EventTracker.AddStartMediaBreadcrumb(mediaItemToStart.MediaType);

        var vm = (MediaViewModel)DataContext;
        vm.VideoRotation = 0;

        switch (mediaItemToStart.MediaType?.Classification)
        {
            case MediaClassification.Image:
                ShowImage(mediaItemToStart);
                break;

            case MediaClassification.Video:
                AdjustVideoRotation(vm, mediaItemToStart);
                mediaItemToStart.PlaybackPositionChangedEvent -= HandleVideoPlaybackPositionChangedEvent;
                await ShowVideoAsync(mediaItemToStart, currentMediaItems, startFromPaused);
                break;

            case MediaClassification.Audio:
                mediaItemToStart.PlaybackPositionChangedEvent -= HandleAudioPlaybackPositionChangedEvent;
                PlayAudio(mediaItemToStart, startFromPaused);
                break;

            case MediaClassification.Slideshow:
                StartSlideshow(mediaItemToStart);
                break;

            case MediaClassification.Web:
                ShowWebPage(mediaItemToStart, currentMediaItems);
                break;
        }
    }

    public void CacheImageItem(MediaItem mediaItem)
    {
        if (mediaItem.FilePath != null)
        {
            _imageDisplayManager.CacheImageItem(mediaItem.FilePath);
        }
    }

    public async Task StopMediaAsync(
        MediaItem mediaItem,
        bool ignoreConfirmation = false)
    {
        if (!ignoreConfirmation && ShouldConfirmMediaStop(mediaItem))
        {
            ConfirmMediaStop(mediaItem);
            return;
        }

        Log.Logger.Information("Stopping media {Path}", mediaItem.FilePath);
        EventTracker.AddStopMediaBreadcrumb(mediaItem.MediaType);

        switch (mediaItem.MediaType?.Classification)
        {
            case MediaClassification.Image:
                HideImage(mediaItem);
                break;

            case MediaClassification.Audio:
                mediaItem.PlaybackPositionChangedEvent -= HandleAudioPlaybackPositionChangedEvent;
                StopAudio(mediaItem);
                break;

            case MediaClassification.Video:
                mediaItem.PlaybackPositionChangedEvent -= HandleVideoPlaybackPositionChangedEvent;
                await HideVideoAsync(mediaItem);
                break;

            case MediaClassification.Slideshow:
                StopSlideshow(mediaItem);
                break;

            case MediaClassification.Web:
                StopWeb();
                break;
        }
    }

    public async Task PauseMediaAsync(MediaItem mediaItem)
    {
        Debug.Assert(
            mediaItem.MediaType?.Classification == MediaClassification.Audio ||
            mediaItem.MediaType?.Classification == MediaClassification.Video,
            "Expecting audio or video media item");

        Log.Logger.Information("Pausing media {Path}", mediaItem.FilePath);

        await PauseVideoOrAudioAsync(mediaItem);
    }

    public int GotoPreviousSlide() => _imageDisplayManager.GotoPreviousSlide();

    public int GotoNextSlide() => _imageDisplayManager.GotoNextSlide();

    public void SaveWindowPos()
    {
        if (IsWindowed)
        {
            _optionsService.MediaWindowPlacement = this.GetPlacement();
            _optionsService.Save();
        }
    }

    public void RestoreWindowPositionAndSize()
    {
        if (IsWindowed && !string.IsNullOrEmpty(_optionsService.MediaWindowPlacement))
        {
            this.SetPlacement(_optionsService.MediaWindowPlacement, _optionsService.MediaWindowSize);
        }
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);

        // If we're not showing a web page, keep focus on the image area so Ctrl+Zoom works
        if ((DataContext as MediaViewModel)?.IsWebPage == false)
        {
            // Slightly defer to ensure layout is ready
            _ = Dispatcher.BeginInvoke(EnsureMediaWindowKeyboardFocus, System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        RestoreWindowPositionAndSize();
        base.OnSourceInitialized(e);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        SaveWindowPos();
        base.OnClosing(e);
    }

    // Note that we must handle Alt+Left and Alt+Right (and any other Alt key combinations) in code behind
    // rather than as KeyBindings in Xaml because WPF treats certain key combinations with Alt as
    // "system keys" (for menu navigation compatibility).
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        var vm = DataContext as MediaViewModel;
        if (vm?.IsWebPage == false)
        {
            return;
        }

        HandleWebKeyDown(e);

        if (!e.Handled)
        {
            base.OnPreviewKeyDown(e);
        }
    }

    private void HandleWebKeyDown(KeyEventArgs e)
    {
        // Handle Alt+Left and Alt+Right as "system keys"
        if (e.KeyboardDevice.Modifiers == ModifierKeys.Alt && e.Key == Key.System)
        {
            if (e.SystemKey == Key.Left)
            {
                if (Browser != null && Browser.BackCommand.CanExecute(null))
                {
                    Browser.BackCommand.Execute(null);
                    e.Handled = true;
                }
            }
            else if (e.SystemKey == Key.Right)
            {
                if (Browser != null && Browser.ForwardCommand.CanExecute(null))
                {
                    Browser.ForwardCommand.Execute(null);
                    e.Handled = true;
                }
            }
            else if (e.SystemKey == Key.Home)
            {
                // Handle Alt+Home to go to the home page
                _webDisplayManager.NavigateToHomeUrl();
                e.Handled = true;
            }
        }
    }

    private async Task PauseVideoOrAudioAsync(MediaItem mediaItem)
    {
        switch (mediaItem.MediaType?.Classification)
        {
            case MediaClassification.Video:
                CheckVideoDisplayManager();
                await _videoDisplayManager!.PauseVideoAsync(mediaItem.Id);
                mediaItem.PlaybackPositionChangedEvent += HandleVideoPlaybackPositionChangedEvent;
                break;

            case MediaClassification.Audio:
                _audioManager.PauseAudio(mediaItem.Id);
                mediaItem.PlaybackPositionChangedEvent += HandleAudioPlaybackPositionChangedEvent;
                break;
        }
    }

    private void CheckVideoDisplayManager()
    {
        if (_videoDisplayManager == null)
        {
            throw new NotSupportedException("Video Display Manager not initialised!");
        }
    }

    private async void HandleVideoPlaybackPositionChangedEvent(object? sender, EventArgs e)
    {
        try
        {
            if (!_optionsService.AllowVideoScrubbing)
            {
                return;
            }

            var item = (MediaItem?)sender;
            if (item == null)
            {
                return;
            }

            CheckVideoDisplayManager();
            await _videoDisplayManager!.SetPlaybackPosition(
                TimeSpan.FromMilliseconds(item.PlaybackPositionDeciseconds * 100));
        }
        catch (Exception ex)
        {
            EventTracker.Error(ex, "Setting video playback position");
            Log.Logger.Error(ex, "Error setting video playback position");
        }
    }

    private void HandleAudioPlaybackPositionChangedEvent(object? sender, EventArgs e)
    {
        if (!_optionsService.AllowVideoScrubbing)
        {
            return;
        }

        var item = (MediaItem?)sender;
        if (item == null)
        {
            return;
        }

        _audioManager.SetPlaybackPosition(
            TimeSpan.FromMilliseconds(item.PlaybackPositionDeciseconds * 100));
    }

    private async Task HideVideoAsync(MediaItem mediaItem)
    {
        CheckVideoDisplayManager();
        await _videoDisplayManager!.HideVideoAsync(mediaItem.Id);
    }

    private void StopAudio(MediaItem mediaItem) => _audioManager.StopAudio(mediaItem.Id);

    private void HideImageOrSlideshow(IReadOnlyCollection<MediaItem> mediaItems)
    {
        var imageItem = mediaItems.SingleOrDefault(
            x => x.MediaType?.Classification == MediaClassification.Image ||
                 x.MediaType?.Classification == MediaClassification.Slideshow);

        if (imageItem == null)
        {
            return;
        }

        switch (imageItem.MediaType?.Classification)
        {
            case MediaClassification.Image:
                _imageDisplayManager.HideSingleImage(imageItem.Id);
                break;

            case MediaClassification.Slideshow:
                _imageDisplayManager.StopSlideshow(imageItem.Id);
                break;
        }
    }

    private void HideImage(MediaItem? mediaItem)
    {
        if (mediaItem != null)
        {
            _imageDisplayManager.HideSingleImage(mediaItem.Id);
        }
    }

    private void ShowImage(MediaItem mediaItem)
    {
        if (mediaItem.FilePath != null)
        {
            EnsureMediaWindowKeyboardFocus();
            _imageDisplayManager.ShowSingleImage(mediaItem.FilePath, mediaItem.Id, mediaItem.IsBlankScreen);
        }
    }

    private void ShowWebPage(MediaItem mediaItem, IReadOnlyCollection<MediaItem>? currentMediaItems)
    {
        if (mediaItem.FilePath == null)
        {
            return;
        }

        var vm = (MediaViewModel)DataContext;
        vm.IsWebPage = !mediaItem.IsPdf;

        // mirror will only work if the media window is full-screen (rather than windowed)
        var showMirrorWindow = mediaItem.UseMirror && mediaItem.AllowUseMirror && !IsWindowed;

        if (!int.TryParse(mediaItem.ChosenPdfPage, out var pdfStartingPage))
        {
            pdfStartingPage = 0;
        }

        _webDisplayManager.ShowWeb(
            mediaItem.FilePath,
            mediaItem.Id,
            pdfStartingPage,
            mediaItem.ChosenPdfViewStyle,
            showMirrorWindow,
            _optionsService.WebScreenPosition);

        // show the header for a few seconds
        _webNavHeaderAdmin.PreviewWebNavHeader();

        if (currentMediaItems != null)
        {
            HideImageOrSlideshow(currentMediaItems);
        }
    }

    private void StopWeb() => _webDisplayManager.HideWeb();

    private void StartSlideshow(MediaItem mediaItem)
    {
        if (mediaItem.FilePath == null)
        {
            return;
        }

        EnsureMediaWindowKeyboardFocus();

        mediaItem.CurrentSlideshowIndex = 0;
        _imageDisplayManager.StartSlideshow(mediaItem.FilePath, mediaItem.Id);
    }

    private void StopSlideshow(MediaItem mediaItem) => _imageDisplayManager.StopSlideshow(mediaItem.Id);

    private async Task ShowVideoAsync(
        MediaItem mediaItemToStart,
        IReadOnlyCollection<MediaItem>? currentMediaItems,
        bool startFromPaused)
    {
        if (mediaItemToStart.FilePath == null)
        {
            return;
        }

        var startPosition = TimeSpan.FromMilliseconds(mediaItemToStart.PlaybackPositionDeciseconds * 100);

        CheckVideoDisplayManager();
        _videoDisplayManager!.ShowSubtitles = _optionsService.ShowVideoSubtitles;

        await _videoDisplayManager.ShowVideoAsync(
            mediaItemToStart.FilePath,
            _optionsService.VideoScreenPosition,
            mediaItemToStart.Id,
            startPosition,
            startFromPaused);

        if (currentMediaItems != null)
        {
            HideImageOrSlideshow(currentMediaItems);
        }
    }

    private void PlayAudio(MediaItem mediaItemToStart, bool startFromPaused)
    {
        if (mediaItemToStart.FilePath == null)
        {
            return;
        }

        var startPosition = TimeSpan.FromMilliseconds(mediaItemToStart.PlaybackPositionDeciseconds * 100);

        _audioManager.PlayAudio(
            mediaItemToStart.FilePath,
            mediaItemToStart.Id,
            startPosition,
            startFromPaused);
    }

    private void WindowClosing(object? sender, CancelEventArgs e)
    {
        // prevent window from being closed independently of application.
        var pageService = Ioc.Default.GetService<IPageService>();
        e.Cancel = pageService != null && !pageService.ApplicationIsClosing && !pageService.AllowMediaWindowToClose;

        if (!e.Cancel)
        {
            UnsubscribeOptionsEvents();
            UnsubscribeImageEvents();
            UnsubscribeVideoEvents();
            UnsubscribeWebEvents();
            UnsubscribeAudioEvents();
        }
    }

    private void SubscribeOptionsEvents()
    {
        _optionsService.ShowSubtitlesChangedEvent += HandleShowSubtitlesChangedEvent;
        _optionsService.ImageScreenPositionChangedEvent += HandleImageScreenPositionChangedEvent;
        _optionsService.VideoScreenPositionChangedEvent += HandleVideoScreenPositionChangedEvent;
        _optionsService.WebScreenPositionChangedEvent += HandleWebScreenPositionChangedEvent;
    }

    private void SubscribeImageEvents()
    {
        _imageDisplayManager.MediaChangeEvent += HandleMediaChangeEvent;
        _imageDisplayManager.SlideTransitionEvent += HandleSlideTransitionEvent;
    }

    private void SubscribeVideoEvents()
    {
        CheckVideoDisplayManager();
        _videoDisplayManager!.MediaChangeEvent += HandleMediaChangeEvent;
        _videoDisplayManager.MediaPositionChangedEvent += HandleMediaPositionChangedEvent;
        _videoDisplayManager.MediaNearEndEvent += HandleMediaNearEndEvent;
        _videoDisplayManager.SubtitleEvent += HandleMediaFoundationSubtitleEvent;
    }

    private void SubscribeWebEvents()
    {
        _webDisplayManager.MediaChangeEvent += HandleMediaChangeEvent;
        _webDisplayManager.StatusEvent += HandleWebDisplayManagerStatusEvent;
    }

    private void SubscribeAudioEvents()
    {
        _audioManager.MediaChangeEvent += HandleMediaChangeEvent;
        _audioManager.MediaPositionChangedEvent += HandleMediaPositionChangedEvent;
    }

    private void UnsubscribeAudioEvents()
    {
        _audioManager.MediaChangeEvent -= HandleMediaChangeEvent;
        _audioManager.MediaPositionChangedEvent -= HandleMediaPositionChangedEvent;
    }

    private void HandleWebDisplayManagerStatusEvent(object? sender, WebBrowserProgressEventArgs e) =>
        WebStatusEvent?.Invoke(this, e);

    private void HandleMediaFoundationSubtitleEvent(object? sender, Core.Subtitles.SubtitleEventArgs e)
    {
        var vm = (MediaViewModel)DataContext;

        vm.SubTitleText = e.Text == null
            ? null
            : string.Join(Environment.NewLine, e.Text);
    }

    private void UnsubscribeOptionsEvents()
    {
        _optionsService.ShowSubtitlesChangedEvent -= HandleShowSubtitlesChangedEvent;
        _optionsService.ImageScreenPositionChangedEvent -= HandleImageScreenPositionChangedEvent;
        _optionsService.VideoScreenPositionChangedEvent -= HandleVideoScreenPositionChangedEvent;
        _optionsService.WebScreenPositionChangedEvent -= HandleWebScreenPositionChangedEvent;
    }

    private void UnsubscribeImageEvents()
    {
        _imageDisplayManager.MediaChangeEvent -= HandleMediaChangeEvent;
        _imageDisplayManager.SlideTransitionEvent -= HandleSlideTransitionEvent;
    }

    private void UnsubscribeVideoEvents()
    {
        CheckVideoDisplayManager();
        _videoDisplayManager!.MediaChangeEvent -= HandleMediaChangeEvent;
        _videoDisplayManager.MediaPositionChangedEvent -= HandleMediaPositionChangedEvent;
        _videoDisplayManager.MediaNearEndEvent -= HandleMediaNearEndEvent;
        _videoDisplayManager.SubtitleEvent -= HandleMediaFoundationSubtitleEvent;
    }

    private void UnsubscribeWebEvents()
    {
        _webDisplayManager.MediaChangeEvent -= HandleMediaChangeEvent;
        _webDisplayManager.StatusEvent -= HandleWebDisplayManagerStatusEvent;
    }

    private void HandleSlideTransitionEvent(object? sender, SlideTransitionEventArgs e) =>
        SlideTransitionEvent?.Invoke(this, e);

    private void HandleMediaPositionChangedEvent(object? sender, OnlyMPositionChangedEventArgs e) =>
        MediaPositionChangedEvent?.Invoke(this, e);

    private void HandleMediaNearEndEvent(object? sender, MediaNearEndEventArgs e) =>
        MediaNearEndEvent?.Invoke(this, e);

    private void HandleShowSubtitlesChangedEvent(object? sender, EventArgs e)
    {
        CheckVideoDisplayManager();
        _videoDisplayManager!.ShowSubtitles = _optionsService.ShowVideoSubtitles;
    }

    private bool ShouldConfirmMediaStop(MediaItem mediaItem)
    {
        switch (mediaItem.MediaType?.Classification)
        {
            case MediaClassification.Video:
                CheckVideoDisplayManager();
                return
                    _optionsService.ConfirmVideoStop &&
                    !_videoDisplayManager!.IsPaused &&
                    _videoDisplayManager.GetPlaybackPosition().TotalSeconds > MediaConfirmStopWindowSeconds;

            case MediaClassification.Audio:
                return
                    _optionsService.ConfirmVideoStop &&
                    !_audioManager.IsPaused &&
                    _audioManager.GetPlaybackPosition().TotalSeconds > MediaConfirmStopWindowSeconds;

            default:
                return false;
        }
    }

    private void ConfirmMediaStop(MediaItem mediaItem) =>
        _snackbarService.Enqueue(
            Properties.Resources.CONFIRM_STOP_MEDIA,
            Properties.Resources.YES,
            // ReSharper disable once AsyncVoidLambda
            async _ => await StopMediaAsync(mediaItem, ignoreConfirmation: true),
            null,
            promote: true,
            neverConsiderToBeDuplicate: true);

    private void HandleVideoScreenPositionChangedEvent(object? sender, EventArgs e)
    {
        if (_videoElement?.FrameworkElement != null)
        {
            ScreenPositionHelper.SetScreenPosition(_videoElement.FrameworkElement, _optionsService.VideoScreenPosition);
            ScreenPositionHelper.SetSubtitleBlockScreenPosition(SubtitleBlock, _optionsService.VideoScreenPosition);
        }
    }

    private void HandleWebScreenPositionChangedEvent(object? sender, EventArgs e) =>
        ScreenPositionHelper.SetScreenPosition(BrowserGrid, _optionsService.WebScreenPosition);

    private void HandleImageScreenPositionChangedEvent(object? sender, EventArgs e)
    {
        ScreenPositionHelper.SetScreenPosition(Image1Element, _optionsService.ImageScreenPosition);
        ScreenPositionHelper.SetScreenPosition(Image2Element, _optionsService.ImageScreenPosition);
    }

    private void InitVideoRenderingMethod()
    {
        _videoElement?.UnsubscribeEvents();

        switch (_optionsService.RenderingMethod)
        {
            case RenderingMethod.Ffmpeg:
                _videoElement = new MediaElementUnoSquare(VideoElementFfmpeg);
                break;

            case RenderingMethod.MediaFoundation:
                _videoElement = new MediaElementMediaFoundation(VideoElementMediaFoundation, _optionsService);
                break;

            default:
                throw new NotSupportedException();
        }

        _currentRenderingMethod = _optionsService.RenderingMethod;

        if (_videoDisplayManager != null)
        {
            UnsubscribeVideoEvents();
            _videoDisplayManager.Dispose();
        }

        _videoDisplayManager = new VideoDisplayManager(_videoElement, SubtitleBlock, _optionsService);

        SubscribeVideoEvents();
    }

    private void WindowSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        var vm = (MediaViewModel)DataContext;
        vm.WindowSize = new Size(ActualWidth, ActualHeight);
    }

    private void BrowserGrid_MouseMove(object? sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(this);
        _webNavHeaderAdmin.MouseMove(pos);
    }

    private void Window_MouseDown(object? sender, MouseButtonEventArgs e)
    {
        // allow drag when no title bar is shown
        if (IsWindowed && e.ChangedButton == MouseButton.Left && WindowStyle == WindowStyle.None)
        {
            DragMove();
        }
    }

    private void AdjustVideoRotation(MediaViewModel vm, MediaItem mediaItemToStart) =>
        // FFMPEG auto rotates video, but Media Foundation doesn't so we 
        // set the required rotation here...
        vm.VideoRotation = _optionsService.RenderingMethod == RenderingMethod.MediaFoundation
            ? mediaItemToStart.VideoRotation
            : 0;

    // Smoothly zoom in centered at the current focus
    private void ZoomInImage_Executed(object? sender, ExecutedRoutedEventArgs e) =>
        ZoomImageTo(_currentImageZoom * ImageZoomStep);

    // Smoothly zoom out centered at the current focus
    private void ZoomOutImage_Executed(object? sender, ExecutedRoutedEventArgs e) =>
        ZoomImageTo(_currentImageZoom / ImageZoomStep);

    private void ResetImageZoom_Executed(object? sender, ExecutedRoutedEventArgs e)
    {
        ResetImageZoomTransforms();
    }

    private void ZoomImage_CanExecute(object? sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = GetActiveImageControl() != null;
    }

    private void ResetImageZoom_CanExecute(object? sender, CanExecuteRoutedEventArgs e)
    {
        if (GetActiveImageControl() == null)
        {
            e.CanExecute = false;
            return;
        }

        var panned = !AreVirtuallyEqual(_currentImageFocus.X, 0.5) ||
                     !AreVirtuallyEqual(_currentImageFocus.Y, 0.5);

        e.CanExecute = _isImageZoomed || panned;
    }

    // allow user to pick focus point by clicking (cursor remains hidden while zoomed)
    private void ImageElement_MouseDown(object? sender, MouseButtonEventArgs e)
    {
        var img = GetActiveImageControl();
        if (img == null || img.Source == null)
        {
            return;
        }

        if (img.Source is not BitmapSource bmp)
        {
            return;
        }

        var p = e.GetPosition(img);
        if (!TryGetContentLayout(img, bmp, out var s, out var left, out var top, out var _, out var _))
        {
            return;
        }

        // Convert hit point to bitmap coordinates (then normalize)
        var px = (p.X - left) / s;
        var py = (p.Y - top) / s;

        px = Math.Clamp(px, 0, bmp.PixelWidth);
        py = Math.Clamp(py, 0, bmp.PixelHeight);

        _currentImageFocus = new Point(px / bmp.PixelWidth, py / bmp.PixelHeight);
    }

    // Reset when image/slideshow starts or stops to avoid carrying zoom between items
    private void HandleMediaChangeEvent(object? sender, MediaEventArgs e)
    {
        MediaChangeEvent?.Invoke(this, e);

        if ((e.Classification == MediaClassification.Image || e.Classification == MediaClassification.Slideshow) &&
            (e.Change == MediaChange.Starting || e.Change == MediaChange.Stopped))
        {
            ResetImageZoomTransforms();
        }
    }

    private void ResetImageZoomTransforms()
    {
        _currentImageZoom = 1.0;
        _currentImageFocus = new Point(0.5, 0.5);
        _isImageZoomed = false;

        ResetTransforms(Image1Element);
        ResetTransforms(Image2Element);

        // Restore cursor when no zoom is applied
        Cursor = Cursors.Arrow;
    }

    private static void ResetTransforms(Image img)
    {
        var (scale, translate) = EnsureImageTransforms(img);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        translate.BeginAnimation(TranslateTransform.XProperty, null);
        translate.BeginAnimation(TranslateTransform.YProperty, null);

        scale.ScaleX = 1.0;
        scale.ScaleY = 1.0;
        translate.X = 0.0;
        translate.Y = 0.0;
    }

    private void ZoomImageTo(double requestedZoom)
    {
        var img = GetActiveImageControl();
        if (img == null || img.Source == null)
        {
            return;
        }

        if (img.Source is not BitmapSource bmp)
        {
            return;
        }

        // Clamp zoom
        var targetZoom = Math.Clamp(requestedZoom, ImageZoomMin, ImageZoomMax);

        if (!TryGetContentLayout(img, bmp, out var s, out var left, out var top, out var cw, out var ch))
        {
            return;
        }

        // Compute focus in bitmap pixels
        var fx = bmp.PixelWidth * _currentImageFocus.X;
        var fy = bmp.PixelHeight * _currentImageFocus.Y;

        // Map focus to control space before transform
        var focusX = left + (fx * s);
        var focusY = top + (fy * s);

        // We apply Scale then Translate (in that order), with origin (0,0)
        // Choose translation so that the focus point goes to the center of the control
        var targetTx = (cw / 2.0) - (focusX * targetZoom);
        var targetTy = (ch / 2.0) - (focusY * targetZoom);

        var (scale, translate) = EnsureImageTransforms(img);

        // Animate
        var animSx = new DoubleAnimation(targetZoom, ImageZoomDuration) { EasingFunction = _imageZoomEase };
        var animSy = new DoubleAnimation(targetZoom, ImageZoomDuration) { EasingFunction = _imageZoomEase };
        var animTx = new DoubleAnimation(targetTx, ImageZoomDuration) { EasingFunction = _imageZoomEase };
        var animTy = new DoubleAnimation(targetTy, ImageZoomDuration) { EasingFunction = _imageZoomEase };

        scale.BeginAnimation(ScaleTransform.ScaleXProperty, animSx);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, animSy);
        translate.BeginAnimation(TranslateTransform.XProperty, animTx);
        translate.BeginAnimation(TranslateTransform.YProperty, animTy);

        _currentImageZoom = targetZoom;
        _isImageZoomed = !AreVirtuallyEqual(_currentImageZoom, 1.0);

        // Hide mouse cursor while zoomed so the audience never sees it
        Cursor = _isImageZoomed ? Cursors.None : Cursors.Arrow;
    }

    private static bool TryGetContentLayout(
        Image img,
        BitmapSource bmp,
        out double s,
        out double left,
        out double top,
        out double cw,
        out double ch)
    {
        cw = img.ActualWidth;
        ch = img.ActualHeight;

        if (cw <= 0 || ch <= 0 || bmp.PixelWidth <= 0 || bmp.PixelHeight <= 0)
        {
            s = left = top = 0;
            return false;
        }

        // Stretch.Uniform layout math (letterboxing)
        s = Math.Min(cw / bmp.PixelWidth, ch / bmp.PixelHeight);
        var dispW = bmp.PixelWidth * s;
        var dispH = bmp.PixelHeight * s;
        left = (cw - dispW) / 2.0;
        top = (ch - dispH) / 2.0;
        return true;
    }

    private static (ScaleTransform scale, TranslateTransform translate) EnsureImageTransforms(Image img)
    {
        ScaleTransform? scale;
        TranslateTransform? translate;

        if (img.RenderTransform is not TransformGroup tg || tg.Children.Count < 2)
        {
            tg = new TransformGroup();
            scale = new ScaleTransform { ScaleX = 1.0, ScaleY = 1.0 };
            translate = new TranslateTransform { X = 0.0, Y = 0.0 };
            tg.Children.Add(scale);
            tg.Children.Add(translate);
            img.RenderTransform = tg;
            img.RenderTransformOrigin = new Point(0, 0);
        }
        else
        {
            scale = tg.Children[0] as ScaleTransform ?? new ScaleTransform();
            translate = tg.Children[1] as TranslateTransform ?? new TranslateTransform();
            tg.Children[0] = scale;
            tg.Children[1] = translate;
        }

        return (scale, translate);
    }

    private Image? GetActiveImageControl()
    {
        // Active image is the one on top (ZIndex = 1) and with a Source
        var img1OnTop = (int)Image1Element.GetValue(Panel.ZIndexProperty) == 1 && Image1Element.Source != null;
        var img2OnTop = (int)Image2Element.GetValue(Panel.ZIndexProperty) == 1 && Image2Element.Source != null;

        if (img1OnTop)
        {
            return Image1Element;
        }

        if (img2OnTop)
        {
            return Image2Element;
        }

        // Fallback to whichever has a Source
        if (Image1Element.Source != null)
        {
            return Image1Element;
        }

        if (Image2Element.Source != null)
        {
            return Image2Element;
        }

        return null;
    }

    private static bool AreVirtuallyEqual(double d1, double d2)
    {
        var difference = Math.Abs(d1 * .00001);
        return Math.Abs(d1 - d2) <= difference;
    }

    // Ensure the window is activated and keyboard focus is within the image area (not the BrowserGrid)
    private void EnsureMediaWindowKeyboardFocus()
    {
        if (!IsVisible || WindowState == WindowState.Minimized)
        {
            return;
        }

        // Bring this window to the foreground and make it the active window
        Activate();
        Focus();

        // Prefer focus on the active image so Window-level KeyBindings are evaluated in this window
        var img = GetActiveImageControl();
        if (img != null)
        {
            if (!img.Focusable)
            {
                img.Focusable = true;
            }

            Keyboard.Focus(img);
        }
        else
        {
            // Fallback: set keyboard focus to the window
            Keyboard.Focus(this);
        }
    }
}
