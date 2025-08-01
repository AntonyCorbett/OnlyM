﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
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
    }

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

    private void HandleMediaChangeEvent(object? sender, MediaEventArgs e) => MediaChangeEvent?.Invoke(this, e);

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
}
