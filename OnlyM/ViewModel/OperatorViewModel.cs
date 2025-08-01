﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OnlyM.Core.Models;
using OnlyM.Core.Services.Media;
using OnlyM.Core.Services.Options;
using OnlyM.Core.Utils;
using OnlyM.CoreSys;
using OnlyM.CoreSys.Services.Snackbar;
using OnlyM.EventTracking;
using OnlyM.MediaElementAdaption;
using OnlyM.Models;
using OnlyM.PubSubMessages;
using OnlyM.Services.Dialogs;
using OnlyM.Services.FrozenVideoItems;
using OnlyM.Services.HiddenMediaItems;
using OnlyM.Services.MediaChanging;
using OnlyM.Services.MetaDataQueue;
using OnlyM.Services.Pages;
using OnlyM.Services.PdfOptions;
using OnlyM.Services.WebBrowser;
using Serilog;

namespace OnlyM.ViewModel;

// ReSharper disable once ClassNeverInstantiated.Global
internal sealed class OperatorViewModel : ObservableObject, IDisposable
{
    private readonly IMediaProviderService _mediaProviderService;
    private readonly IThumbnailService _thumbnailService;
    private readonly IMediaMetaDataService _metaDataService;
    private readonly IOptionsService _optionsService;
    private readonly IPageService _pageService;
    private readonly IMediaStatusChangingService _mediaStatusChangingService;
    private readonly IHiddenMediaItemsService _hiddenMediaItemsService;
    private readonly IFrozenVideosService _frozenVideosService;
    private readonly IPdfOptionsService _pdfOptionsService;
    private readonly IActiveMediaItemsService _activeMediaItemsService;
    private readonly ISnackbarService _snackbarService;
    private readonly IDialogService _dialogService;

    private readonly MetaDataQueueProducer _metaDataProducer = new();
    private readonly CancellationTokenSource _metaDataCancellationTokenSource = new();

    private MetaDataQueueConsumer? _metaDataConsumer;
    private string? _blankScreenImagePath;
    private bool _pendingLoadMediaItems;
    private int _thumbnailColWidth = 180;

    public OperatorViewModel(
        IMediaProviderService mediaProviderService,
        IThumbnailService thumbnailService,
        IMediaMetaDataService metaDataService,
        IOptionsService optionsService,
        IPageService pageService,
        IFolderWatcherService folderWatcherService,
        IMediaStatusChangingService mediaStatusChangingService,
        IHiddenMediaItemsService hiddenMediaItemsService,
        IActiveMediaItemsService activeMediaItemsService,
        IFrozenVideosService frozenVideosService,
        IPdfOptionsService pdfOptionsService,
        ISnackbarService snackbarService,
        IDialogService dialogService)
    {
        _mediaProviderService = mediaProviderService;
        _mediaStatusChangingService = mediaStatusChangingService;

        _hiddenMediaItemsService = hiddenMediaItemsService;
        _hiddenMediaItemsService.UnhideAllEvent += HandleUnhideAllEvent;

        _snackbarService = snackbarService;
        _dialogService = dialogService;

        _activeMediaItemsService = activeMediaItemsService;
        _frozenVideosService = frozenVideosService;
        _pdfOptionsService = pdfOptionsService;

        _thumbnailService = thumbnailService;
        _thumbnailService.ThumbnailsPurgedEvent += HandleThumbnailsPurgedEvent;

        _metaDataService = metaDataService;

        _optionsService = optionsService;
        _optionsService.MediaFolderChangedEvent += HandleMediaFolderChangedEvent;
        _optionsService.AutoRotateChangedEvent += HandleAutoRotateChangedEvent;
        _optionsService.AllowVideoPauseChangedEvent += HandleAllowVideoPauseChangedEvent;
        _optionsService.AllowVideoPositionSeekingChangedEvent += HandleAllowVideoPositionSeekingChangedEvent;
        _optionsService.UseInternalMediaTitlesChangedEvent += HandleUseInternalMediaTitlesChangedEvent;
        _optionsService.ShowMediaItemCommandPanelChangedEvent += HandleShowMediaItemCommandPanelChangedEvent;
        _optionsService.AllowMirrorChangedEvent += HandleAllowMirrorChangedEvent;
        _optionsService.ShowFreezeCommandChangedEvent += HandleShowFreezeCommandChangedEvent;
        _optionsService.OperatingDateChangedEvent += HandleOperatingDateChangedEvent;
        _optionsService.MaxItemCountChangedEvent += HandleMaxItemCountChangedEvent;
        _optionsService.PermanentBackdropChangedEvent += async (_, _) => await HandlePermanentBackdropChangedEvent();
        _optionsService.IncludeBlankScreenItemChangedEvent += async (_, _) => await HandleIncludeBlankScreenItemChangedEvent();

        folderWatcherService.ChangesFoundEvent += HandleFileChangesFoundEvent;

        _pageService = pageService;
        _pageService.MediaChangeEvent += HandleMediaChangeEvent;
        _pageService.SlideTransitionEvent += HandleSlideTransitionEvent;
        _pageService.MediaMonitorChangedEvent += HandleMediaMonitorChangedEvent;
        _pageService.MediaPositionChangedEvent += HandleMediaPositionChangedEvent;
        _pageService.MediaNearEndEvent += async (_, e) => await HandleMediaNearEndEvent(e);
        _pageService.NavigationEvent += HandleNavigationEvent;
        _pageService.WebStatusEvent += HandleWebStatusEvent;

        LoadMediaItems();
        InitCommands();

        LaunchThumbnailQueueConsumer();

        WeakReferenceMessenger.Default.Register<ShutDownMessage>(this, OnShutDown);
        WeakReferenceMessenger.Default.Register<SubtitleFileMessage>(this, OnSubtitleFileActivity);
    }

    public ObservableCollectionEx<MediaItem> MediaItems { get; } = [];

    public AsyncRelayCommand<Guid?> MediaControlCommand1 { get; private set; } = null!;

    public AsyncRelayCommand<Guid?> MediaControlPauseCommand { get; private set; } = null!;

    public RelayCommand<Guid?> HideMediaItemCommand { get; private set; } = null!;

    public RelayCommand<Guid?> DeleteMediaItemCommand { get; private set; } = null!;

    public RelayCommand<Guid?> OpenCommandPanelCommand { get; private set; } = null!;

    public RelayCommand<Guid?> CloseCommandPanelCommand { get; private set; } = null!;

    public RelayCommand<Guid?> FreezeVideoCommand { get; private set; } = null!;

    public RelayCommand<Guid?> PreviousSlideCommand { get; private set; } = null!;

    public RelayCommand<Guid?> NextSlideCommand { get; private set; } = null!;

    public RelayCommand<Guid?> EnterStartOffsetEditModeCommand { get; private set; } = null!;

    public int ThumbnailColWidth
    {
        get => _thumbnailColWidth;
        set => SetProperty(ref _thumbnailColWidth, value);
    }

    public void Dispose()
    {
        _metaDataProducer.Dispose();
        _metaDataCancellationTokenSource.Dispose();
        _metaDataConsumer?.Dispose();
    }

    private void HandleMaxItemCountChangedEvent(object? sender, EventArgs e) =>
        _pendingLoadMediaItems = true;

    private void HandleNavigationEvent(object? sender, NavigationEventArgs e)
    {
        if (e.PageName == null || !e.PageName.Equals(_pageService.OperatorPageName, StringComparison.Ordinal))
        {
            return;
        }

        if (!_pendingLoadMediaItems)
        {
            return;
        }

        _pendingLoadMediaItems = false;
        LoadMediaItems();
    }

    private void HandleOperatingDateChangedEvent(object? sender, EventArgs e) =>
        _pendingLoadMediaItems = true;

    private void HandleUnhideAllEvent(object? sender, EventArgs e)
    {
        using (new ObservableCollectionSuppression<MediaItem>(MediaItems))
        {
            foreach (var item in MediaItems)
            {
                item.IsVisible = true;
            }
        }
    }

    private void HandleShowMediaItemCommandPanelChangedEvent(object? sender, EventArgs e)
    {
        var visible = _optionsService.ShowMediaItemCommandPanel;
        using (new ObservableCollectionSuppression<MediaItem>(MediaItems))
        {
            foreach (var item in MediaItems)
            {
                item.CommandPanelVisible = visible;
            }
        }
    }

    private void HandleShowFreezeCommandChangedEvent(object? sender, EventArgs e)
    {
        var allow = _optionsService.ShowFreezeCommand;

        foreach (var item in MediaItems)
        {
            item.AllowFreezeCommand = allow;
        }
    }

    private async Task HandleIncludeBlankScreenItemChangedEvent()
    {
        if (!_optionsService.IncludeBlankScreenItem)
        {
            await RemoveBlankScreenItem();
        }

        _pendingLoadMediaItems = true;
    }

    private async Task HandlePermanentBackdropChangedEvent()
    {
        if (_optionsService.PermanentBackdrop)
        {
            await RemoveBlankScreenItem();
        }

        _pendingLoadMediaItems = true;
    }

    private async Task RemoveBlankScreenItem()
    {
        // before removing the blank screen item, hide it if showing.
        var blankScreenItem = GetActiveBlankScreenItem();
        if (blankScreenItem != null)
        {
            await _pageService.StopMediaAsync(blankScreenItem);
        }
    }

    private MediaItem? GetActiveBlankScreenItem()
    {
        var items = GetCurrentMediaItems();
        return items?.SingleOrDefault(x => x.IsBlankScreen && x.IsMediaActive);
    }

    private MediaItem? GetActiveWebItem()
    {
        var items = GetCurrentMediaItems();
        return items?.SingleOrDefault(x => x.IsWeb && x.IsMediaActive);
    }

    private void HandleUseInternalMediaTitlesChangedEvent(object? sender, EventArgs e)
    {
        foreach (var item in MediaItems)
        {
            item.Title = null;
        }

        FillThumbnailsAndMetaData();

        _pendingLoadMediaItems = true;
    }

    private void HandleAllowVideoPositionSeekingChangedEvent(object? sender, EventArgs e)
    {
        foreach (var item in MediaItems)
        {
            item.AllowPositionSeeking = _optionsService.AllowVideoPositionSeeking;
        }
    }

    private void HandleAllowVideoPauseChangedEvent(object? sender, EventArgs e)
    {
        foreach (var item in MediaItems)
        {
            item.AllowPause = _optionsService.AllowVideoPause;
        }
    }

    private async Task HandleMediaNearEndEvent(MediaNearEndEventArgs e)
    {
        var item = GetMediaItem(e.MediaItemId);
        if (item != null && item.PauseOnLastFrame)
        {
            await MediaPauseControl(item.Id);
        }
    }

    private void HandleMediaPositionChangedEvent(object? sender, OnlyMPositionChangedEventArgs e)
    {
        var item = GetMediaItem(e.MediaItemId);
        if (item != null && !item.IsPaused)
        {
            item.PlaybackPositionDeciseconds = (int)(e.Position.TotalMilliseconds / 100);
        }
    }

    private void OnShutDown(object? sender, ShutDownMessage message) =>
        // cancel the thumbnail consumer thread.
        _metaDataCancellationTokenSource.Cancel();

    private void LaunchThumbnailQueueConsumer()
    {
        _metaDataConsumer = new MetaDataQueueConsumer(
            _thumbnailService,
            _metaDataService,
            _optionsService,
            _metaDataProducer.Queue,
            App.FMpegFolderName,
            _metaDataCancellationTokenSource.Token);

        _metaDataConsumer.ItemCompletedEvent += HandleItemCompletedEvent;

        _metaDataConsumer.Execute();
    }

    private void HandleItemCompletedEvent(object? sender, ItemMetaDataPopulatedEventArgs e)
    {
        var item = e.MediaItem;
        if (item == null)
        {
            return;
        }

        if (_optionsService.AutoRotateImages)
        {
            AutoRotateImageIfRequired(item);
        }
    }

    private void HandleFileChangesFoundEvent(object? sender, EventArgs e) =>
        Application.Current.Dispatcher.Invoke(LoadMediaItems);

    private void HandleMediaMonitorChangedEvent(object? sender, MonitorChangedEventArgs e) =>
        ChangePlayButtonEnabledStatus();

    private void ChangePlayButtonEnabledStatus()
    {
        var monitorSpecified = _optionsService.IsMediaMonitorSpecified || _optionsService.MediaWindowed;
        var videoOrAudioIsActive = VideoOrAudioIsActive();
        var videoIsActive = VideoIsActive();
        var rollingSlideshowIsActive = RollingSlideshowIsActive();
        var webIsActive = WebIsActive();

        foreach (var item in MediaItems)
        {
            switch (item.MediaType?.Classification)
            {
                case MediaClassification.Image:
                    // cannot show an image if video or rolling slideshow or web page is playing.
                    item.IsPlayButtonEnabled =
                        monitorSpecified &&
                        !videoIsActive &&
                        !rollingSlideshowIsActive &&
                        !webIsActive;
                    break;

                case MediaClassification.Audio:
                    // cannot start audio if another video or audio is playing.
                    item.IsPlayButtonEnabled = !videoOrAudioIsActive;
                    break;

                case MediaClassification.Video:
                    // cannot play a video if another video or audio or rolling slideshow or web page is playing.
                    item.IsPlayButtonEnabled =
                        monitorSpecified &&
                        !videoOrAudioIsActive &&
                        !rollingSlideshowIsActive &&
                        !webIsActive;
                    break;

                case MediaClassification.Slideshow:
                    // cannot play a slideshow if video or rolling slideshow or web page is playing.
                    item.IsPlayButtonEnabled =
                        monitorSpecified &&
                        !videoIsActive &&
                        !rollingSlideshowIsActive &&
                        !webIsActive;
                    break;

                case MediaClassification.Web:
                    // cannot launch a web page if video or rolling slideshow or web page is playing.
                    item.IsPlayButtonEnabled =
                        monitorSpecified &&
                        !videoIsActive &&
                        !rollingSlideshowIsActive &&
                        !webIsActive;
                    break;

                default:
                    item.IsPlayButtonEnabled = false;
                    break;
            }
        }
    }

    private void HandleSlideTransitionEvent(object? sender, SlideTransitionEventArgs e)
    {
        Log.Debug("HandleSlideTransitionEvent (id = {MediaItemId}, change = {Transition})", e.MediaItemId, e.Transition);

        var mediaItem = GetMediaItem(e.MediaItemId);
        if (mediaItem == null)
        {
            return;
        }

        switch (e.Transition)
        {
            case SlideTransition.Started:
                mediaItem.IsMediaChanging = true;
                break;

            case SlideTransition.Finished:
                mediaItem.IsMediaChanging = false;
                mediaItem.CurrentSlideshowIndex = e.NewSlideIndex;
                break;
        }
    }

    private void HandleMediaChangeEvent(object? sender, MediaEventArgs e)
    {
        Log.Debug("HandleMediaChangeEvent (id = {MediaItemId}, change = {Change})", e.MediaItemId, e.Change);

        var mediaItem = GetMediaItem(e.MediaItemId);
        if (mediaItem == null)
        {
            return;
        }

        Log.Debug("Title={Title}", mediaItem.Title ?? "untitled");

        switch (e.Change)
        {
            case MediaChange.Starting:
                mediaItem.IsMediaActive = true;
                mediaItem.IsMediaChanging = true;
                _mediaStatusChangingService.AddChangingItem(mediaItem.Id);
                break;

            case MediaChange.Stopping:
                mediaItem.IsMediaActive = false;
                mediaItem.IsPaused = false;
                mediaItem.IsMediaChanging = true;
                _mediaStatusChangingService.AddChangingItem(mediaItem.Id);
                break;

            case MediaChange.Started:
                mediaItem.IsMediaActive = true;
                mediaItem.IsMediaChanging = false;
                mediaItem.IsPaused = false;
                _mediaStatusChangingService.RemoveChangingItem(mediaItem.Id);
                break;

            case MediaChange.Stopped:
                mediaItem.IsMediaActive = false;
                mediaItem.IsMediaChanging = false;
                mediaItem.PlaybackPositionDeciseconds = 0;
                _mediaStatusChangingService.RemoveChangingItem(mediaItem.Id);
                break;

            case MediaChange.Paused:
                mediaItem.IsPaused = true;
                break;
        }

        ChangePlayButtonEnabledStatus();
    }

    private void InitCommands()
    {
        MediaControlCommand1 = new AsyncRelayCommand<Guid?>(MediaControl1);

        MediaControlPauseCommand = new AsyncRelayCommand<Guid?>(MediaPauseControl);

        HideMediaItemCommand = new RelayCommand<Guid?>(HideMediaItem);

        DeleteMediaItemCommand = new RelayCommand<Guid?>(DeleteMediaItem);

        OpenCommandPanelCommand = new RelayCommand<Guid?>(OpenCommandPanel);

        CloseCommandPanelCommand = new RelayCommand<Guid?>(CloseCommandPanel);

        FreezeVideoCommand = new RelayCommand<Guid?>(FreezeVideoOnLastFrame);

        PreviousSlideCommand = new RelayCommand<Guid?>(GotoPreviousSlide);

        NextSlideCommand = new RelayCommand<Guid?>(GotoNextSlide);

        EnterStartOffsetEditModeCommand = new RelayCommand<Guid?>(EnterStartOffsetEditMode);
    }

    // Exceptions handled
    private async void EnterStartOffsetEditMode(Guid? mediaItemId)
    {
        try
        {
            await InternalEnterStartOffsetEditMode(mediaItemId);
        }
        catch (Exception ex)
        {
            EventTracker.Error(ex, "Editing start offset");
            Log.Logger.Error(ex, "Could not enter StartOffset mode");
        }
    }

    private async Task InternalEnterStartOffsetEditMode(Guid? mediaItemId)
    {
        if (mediaItemId == null)
        {
            return;
        }

        var mediaItem = GetMediaItem(mediaItemId.Value);
        if (mediaItem?.FilePath != null && (!mediaItem.IsMediaActive || mediaItem.IsPaused))
        {
            var maxStartTimeSeconds = (int)((double)mediaItem.DurationDeciseconds / 10);

            var start = await _dialogService.GetStartOffsetAsync(
                Path.GetFileName(mediaItem.FilePath),
                maxStartTimeSeconds);

            if (start != null)
            {
                var deciSecs = (int)(start.Value.TotalSeconds * 10);
                if (deciSecs < mediaItem.DurationDeciseconds)
                {
                    mediaItem.PlaybackPositionDeciseconds = deciSecs;
                }
            }
        }
    }

    private void FreezeVideoOnLastFrame(Guid? mediaItemId)
    {
        if (mediaItemId == null)
        {
            return;
        }

        var mediaItem = GetMediaItem(mediaItemId.Value);
        if (mediaItem?.FilePath == null)
        {
            return;
        }

        Debug.Assert(
            mediaItem.MediaType?.Classification == MediaClassification.Video,
            "mediaItem.MediaType.Classification == MediaClassification.Video");

        if (mediaItem.PauseOnLastFrame)
        {
            _frozenVideosService.Add(mediaItem.FilePath);
        }
        else
        {
            _frozenVideosService.Remove(mediaItem.FilePath);
        }
    }

    private void CloseCommandPanel(Guid? mediaItemId)
    {
        if (mediaItemId == null)
        {
            return;
        }

        var mediaItem = GetMediaItem(mediaItemId.Value);
        if (mediaItem != null)
        {
            SavePdfOptions(mediaItem);
            mediaItem.IsCommandPanelOpen = false;
        }
    }

    private void SavePdfOptions(MediaItem mediaItem)
    {
        if (mediaItem.FilePath == null)
        {
            return;
        }

        _pdfOptionsService.Add(
            mediaItem.FilePath,
            new PdfOptions
            {
                PageNumber = Convert.ToInt32(mediaItem.ChosenPdfPage, CultureInfo.InvariantCulture),
                Style = mediaItem.ChosenPdfViewStyle,
            });
    }

    private void OpenCommandPanel(Guid? mediaItemId)
    {
        if (mediaItemId == null)
        {
            return;
        }

        var mediaItem = GetMediaItem(mediaItemId.Value);
        if (mediaItem != null)
        {
            mediaItem.IsCommandPanelOpen = true;
        }
    }

    private void GotoPreviousSlide(Guid? mediaItemId)
    {
        if (mediaItemId == null)
        {
            return;
        }

        if (_mediaStatusChangingService.IsMediaStatusChanging())
        {
            return;
        }

        var mediaItem = GetMediaItem(mediaItemId.Value);
        if (mediaItem != null && !mediaItem.IsMediaChanging)
        {
            mediaItem.CurrentSlideshowIndex = _pageService.GotoPreviousSlide();
        }
    }

    private void GotoNextSlide(Guid? mediaItemId)
    {
        if (mediaItemId == null)
        {
            return;
        }

        if (_mediaStatusChangingService.IsMediaStatusChanging())
        {
            return;
        }

        var mediaItem = GetMediaItem(mediaItemId.Value);
        if (mediaItem != null && !mediaItem.IsMediaChanging)
        {
            mediaItem.CurrentSlideshowIndex = _pageService.GotoNextSlide();
        }
    }

    private void HideMediaItem(Guid? mediaItemId)
    {
        if (mediaItemId == null)
        {
            return;
        }

        var mediaItem = GetMediaItem(mediaItemId.Value);
        if (mediaItem?.FilePath == null)
        {
            return;
        }

        mediaItem.IsCommandPanelOpen = false;

        Task.Delay(400).ContinueWith(_ =>
        {
            mediaItem.IsVisible = false;
            _hiddenMediaItemsService.Add(mediaItem.FilePath);
        });
    }

    private void DeleteMediaItem(Guid? mediaItemId)
    {
        if (mediaItemId == null)
        {
            return;
        }

        var mediaItem = GetMediaItem(mediaItemId.Value);
        if (mediaItem?.FilePath == null)
        {
            return;
        }

        if (FileUtils.SafeDeleteFile(mediaItem.FilePath))
        {
            mediaItem.IsCommandPanelOpen = false;
        }
        else
        {
            _snackbarService.EnqueueWithOk(Properties.Resources.CANNOT_DELETE_FILE, Properties.Resources.OK);
        }
    }

    private bool IsVideoOrAudio(MediaItem mediaItem) =>
        mediaItem.MediaType?.Classification == MediaClassification.Audio ||
        mediaItem.MediaType?.Classification == MediaClassification.Video;

    // ReSharper disable once MemberCanBeMadeStatic.Local
    private bool IsVideo(MediaItem mediaItem) => mediaItem.IsVideo;

    // ReSharper disable once MemberCanBeMadeStatic.Local
    private bool IsRollingSlideshow(MediaItem mediaItem) => mediaItem.IsRollingSlideshow;

    // ReSharper disable once MemberCanBeMadeStatic.Local
    private bool IsWeb(MediaItem mediaItem) => mediaItem.IsWeb;

    private async Task MediaPauseControl(Guid? mediaItemId)
    {
        if (mediaItemId == null)
        {
            return;
        }

        try
        {
            await MediaPauseControlInternal(mediaItemId.Value);
        }
        catch (Exception ex)
        {
            EventTracker.Error(ex, "Pausing media");
            Log.Error(ex, "Pause control");
        }
    }

    private async Task MediaPauseControlInternal(Guid mediaItemId)
    {
        // only allow pause media when nothing is changing.
        if (!_mediaStatusChangingService.IsMediaStatusChanging())
        {
            var mediaItem = GetMediaItem(mediaItemId);
            if (mediaItem == null || !IsVideoOrAudio(mediaItem))
            {
                Log.Error("Media Item not found (id = {MediaItemId})", mediaItemId);
                return;
            }

            if (mediaItem.IsMediaActive)
            {
                if (mediaItem.IsPaused)
                {
                    var items = GetCurrentMediaItems();
                    if (items != null)
                    {
                        await _pageService.StartMedia(mediaItem, items, true);
                    }
                    else
                    {
                        Log.Warning("Anomaly - no media items!");
                    }
                }
                else
                {
                    await _pageService.PauseMediaAsync(mediaItem);
                }
            }
        }
    }

    private async Task MediaControl1(Guid? mediaItemId)
    {
        if (mediaItemId == null)
        {
            return;
        }

        try
        {
            await MediaControl1Internal(mediaItemId.Value);
        }
        catch (Exception ex)
        {
            EventTracker.Error(ex, "Media control 1");
            Log.Error(ex, "Media control1");
        }
    }

    private async Task MediaControl1Internal(Guid mediaItemId)
    {
        // only allow start/stop media when nothing is changing.
        if (!_mediaStatusChangingService.IsMediaStatusChanging())
        {
            Log.Debug("MediaControl1 (id = {MediaItemId})", mediaItemId);

            var mediaItem = GetMediaItem(mediaItemId);
            if (mediaItem == null)
            {
                Log.Error("Media Item not found (id = {MediaItemId})", mediaItemId);
                return;
            }

            if (mediaItem.IsMediaActive)
            {
                await _pageService.StopMediaAsync(mediaItem);
            }
            else
            {
                if (CanStartMedia(mediaItem))
                {
                    await _pageService.StartMedia(mediaItem, GetCurrentMediaItems(), false);

                    // when displaying an item we ensure that the next image item is cached.
                    _pageService.CacheImageItem(GetNextImageItem(mediaItem));
                }
            }
        }
    }

    private bool CanStartMedia(MediaItem item)
    {
        switch (item.MediaType?.Classification)
        {
            case MediaClassification.Audio:
            case MediaClassification.Video:
                return !VideoOrAudioIsActive();

            case MediaClassification.Image:
            case MediaClassification.Slideshow:
            case MediaClassification.Web:
                return !VideoIsActive();

            default:
                return false;
        }
    }

    private List<MediaItem>? GetCurrentMediaItems()
    {
        if (!_activeMediaItemsService.Any())
        {
            return null;
        }

        return _activeMediaItemsService.GetMediaItemIds().Select(GetMediaItem).Where(x => x != null).ToList()!;
    }

    private bool VideoOrAudioIsActive()
    {
        var currentItems = GetCurrentMediaItems();
        return currentItems != null && currentItems.Any(IsVideoOrAudio);
    }

    private bool VideoIsActive()
    {
        var currentItems = GetCurrentMediaItems();
        return currentItems != null && currentItems.Any(IsVideo);
    }

    private bool RollingSlideshowIsActive()
    {
        var currentItems = GetCurrentMediaItems();
        return currentItems != null && currentItems.Any(IsRollingSlideshow);
    }

    private bool WebIsActive()
    {
        var currentItems = GetCurrentMediaItems();
        return currentItems != null && currentItems.Any(IsWeb);
    }

    private MediaItem? GetNextImageItem(MediaItem? currentMediaItem)
    {
        if (currentMediaItem == null)
        {
            return null;
        }

        var found = false;
        foreach (var item in MediaItems)
        {
            if (found && item.MediaType?.Classification == MediaClassification.Image)
            {
                return item;
            }

            if (item == currentMediaItem)
            {
                found = true;
            }
        }

        return null;
    }

    private MediaItem? GetMediaItem(Guid mediaItemId) =>
        MediaItems.SingleOrDefault(x => x.Id == mediaItemId);

    private void HandleMediaFolderChangedEvent(object? sender, EventArgs e) =>
        _pendingLoadMediaItems = true;

    private void HandleThumbnailsPurgedEvent(object? sender, EventArgs e)
    {
        using (new ObservableCollectionSuppression<MediaItem>(MediaItems))
        {
            foreach (var item in MediaItems)
            {
                item.ThumbnailImageSource = null;
            }

            FillThumbnailsAndMetaData();
        }
    }

    private void LoadMediaItems()
    {
        if (IsInDesignMode())
        {
            return;
        }

        Log.Logger.Debug("Loading media items");

        WeakReferenceMessenger.Default.Send(new MediaListUpdatingMessage());

        using (new ObservableCollectionSuppression<MediaItem>(MediaItems))
        {
            LoadMediaItemsInternal();
        }

        ChangePlayButtonEnabledStatus();

        WeakReferenceMessenger.Default.Send(new MediaListUpdatedMessage { Count = MediaItems.Count });

        Log.Logger.Debug("Completed loading media items");
    }

    private void LoadMediaItemsInternal()
    {
        var files = _mediaProviderService.GetMediaFiles();
        var sourceFilePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var destFilePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            if (file.FullPath != null)
            {
                sourceFilePaths.Add(file.FullPath);
            }
        }

        var itemsToRemove = new List<MediaItem>();

        foreach (var item in MediaItems)
        {
            var filePath = item.FilePath;

            if (filePath == null)
            {
                continue;
            }

            if (!sourceFilePaths.Contains(filePath))
            {
                // remove this item.
                itemsToRemove.Add(item);
            }
            else
            {
                // perhaps the item has been changed or swapped for another media file of the same name!
                var file = files.SingleOrDefault(x => x.FullPath == filePath);
                if (file != null && file.LastChanged != item.LastChanged)
                {
                    // remove this item.
                    itemsToRemove.Add(item);
                }
                else
                {
                    destFilePaths.Add(filePath);
                }
            }
        }

        var currentItems = GetCurrentMediaItems(); // currently playing
        var deletedCurrentItems = currentItems?.Intersect(itemsToRemove).ToArray();
        var count = deletedCurrentItems?.Length ?? 0;
        if (count > 0)
        {
            // we have deleted one or more items that are currently being displayed!
            Log.Logger.Warning("User deleted {Count} active items", count);

            ForciblyStopAllPlayback(currentItems);

            _snackbarService.EnqueueWithOk(Properties.Resources.ACTIVE_ITEM_DELETED, Properties.Resources.OK);
        }

        // remove old items.
        foreach (var item in itemsToRemove)
        {
            MediaItems.Remove(item);
        }

        // add new items.
        foreach (var file in files)
        {
            if (file.FullPath != null && !destFilePaths.Contains(file.FullPath))
            {
                var item = CreateNewMediaItem(file);

                MediaItems.Add(item);

                _metaDataProducer.Add(item);
            }
        }

        TruncateMediaItemsToMaxCount();

        _hiddenMediaItemsService.Init(MediaItems);
        _frozenVideosService.Init(MediaItems);
        _pdfOptionsService.Init(MediaItems);

        SortMediaItems();

        InsertBlankMediaItem();
    }

    private void ForciblyStopAllPlayback(List<MediaItem>? activeItems)
    {
        if (activeItems == null)
        {
            return;
        }

        _pageService.ForciblyStopAllPlayback(activeItems);

        foreach (var item in activeItems)
        {
            _mediaStatusChangingService.RemoveChangingItem(item.Id);
            _activeMediaItemsService.Remove(item.Id);
        }
    }

    private void TruncateMediaItemsToMaxCount()
    {
        while (MediaItems.Count > _optionsService.MaxItemCount)
        {
            MediaItems.RemoveAt(MediaItems.Count - 1);
        }
    }

    private MediaItem CreateNewMediaItem(MediaFile file)
    {
        var result = new MediaItem
        {
            MediaType = file.MediaType,
            Id = Guid.NewGuid(),
            AllowFreezeCommand = _optionsService.ShowFreezeCommand,
            CommandPanelVisible = _optionsService.ShowMediaItemCommandPanel,
            FilePath = file.FullPath,
            IsVisible = true,
            LastChanged = file.LastChanged,
            AllowPause = _optionsService.AllowVideoPause,
            AllowPositionSeeking = _optionsService.AllowVideoPositionSeeking,
            AllowUseMirror = _optionsService.AllowMirror,
            UseMirror = _optionsService.UseMirrorByDefault,
        };

        return result;
    }

    private void InsertBlankMediaItem()
    {
        // only add a "blank screen" item if we don't already display
        // a permanent black backdrop.
        if (_optionsService.PermanentBackdrop || !_optionsService.IncludeBlankScreenItem)
        {
            return;
        }

        var blankScreenPath = GetBlankScreenPath();

        var item = new MediaItem
        {
            MediaType = _mediaProviderService.GetSupportedMediaType(blankScreenPath),
            Id = Guid.NewGuid(),
            IsBlankScreen = true,
            IsVisible = true,
            AllowFreezeCommand = _optionsService.ShowFreezeCommand,
            CommandPanelVisible = _optionsService.ShowMediaItemCommandPanel,
            Title = Properties.Resources.BLANK_SCREEN,
            FilePath = blankScreenPath,
            FileNameAsSubTitle = null,
            LastChanged = 0,
        };

        MediaItems.Insert(0, item);

        _metaDataProducer.Add(item);
    }

    private string? GetBlankScreenPath()
    {
        if (_blankScreenImagePath != null)
        {
            return _blankScreenImagePath;
        }

        try
        {
            var tempFolder = Path.Combine(FileUtils.GetUsersTempFolder(), "OnlyM", "TempImages");
            FileUtils.CreateDirectory(tempFolder);

            var path = Path.Combine(tempFolder, "BlankScreen.png");

            var image = Properties.Resources.blank;

            // overwrites it each time OnlyM is launched
            image.Save(path);

            _blankScreenImagePath = path;
        }
        catch (Exception ex)
        {
            EventTracker.Error(ex, "Saving blank screen image");
            Log.Logger.Error(ex, "Could not save blank screen image");
        }

        return _blankScreenImagePath;
    }

    private void SortMediaItems()
    {
        var sorted = MediaItems.OrderBy(x => x.SortKey).ToList();
        var blank = sorted.SingleOrDefault(x => x.IsBlankScreen);
        if (blank != null && blank != sorted.First())
        {
            sorted.Remove(blank);
            sorted.Insert(0, blank);
        }

        for (var n = 0; n < sorted.Count; ++n)
        {
            MediaItems.Move(MediaItems.IndexOf(sorted[n]), n);
        }
    }

    private void FillThumbnailsAndMetaData()
    {
        foreach (var item in MediaItems)
        {
            _metaDataProducer.Add(item);
        }
    }

    private void AutoRotateImageIfRequired(MediaItem item)
    {
        if (item.MediaType?.Classification != MediaClassification.Image)
        {
            return;
        }

        if (!GraphicsUtils.AutoRotateIfRequired(item.FilePath))
        {
            return;
        }

        // auto-rotated so refresh the thumbnail...
        item.ThumbnailImageSource = null;
        item.LastChanged = DateTime.UtcNow.Ticks;
        _metaDataProducer.Add(item);
    }

    private void HandleAutoRotateChangedEvent(object? sender, EventArgs e) =>
        Task.Run(() =>
        {
            try
            {
                if (_optionsService.AutoRotateImages)
                {
                    foreach (var item in MediaItems)
                    {
                        AutoRotateImageIfRequired(item);
                    }
                }

                _pendingLoadMediaItems = true;
            }
            catch (Exception ex)
            {
                EventTracker.Error(ex, "Rotating image");
                Log.Logger.Error(ex, "Auto rotation of images");
            }
        });

    private void OnSubtitleFileActivity(object? sender, SubtitleFileMessage message)
    {
        if (message.Starting)
        {
            _snackbarService.EnqueueWithOk(Properties.Resources.GENERATING_SUBTITLES, Properties.Resources.OK);
        }
    }

    private void HandleWebStatusEvent(object? sender, WebBrowserProgressEventArgs e) =>
        Application.Current.Dispatcher.Invoke(() =>
        {
            var item = GetActiveWebItem();
            if (item != null)
            {
                item.MiscText = e.Description;
            }
        });

    private void HandleAllowMirrorChangedEvent(object? sender, EventArgs e)
    {
        var allow = _optionsService.AllowMirror;

        foreach (var item in MediaItems)
        {
            item.AllowUseMirror = allow;
        }
    }

    private static bool IsInDesignMode()
    {
#if DEBUG
        var dep = new DependencyObject();
        return DesignerProperties.GetIsInDesignMode(dep);
#else
            return false;
#endif
    }
}
