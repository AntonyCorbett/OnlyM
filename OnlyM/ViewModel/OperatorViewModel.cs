namespace OnlyM.ViewModel
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Core.Models;
    using Core.Services.Media;
    using Core.Services.Options;
    using Core.Utils;
    using GalaSoft.MvvmLight;
    using GalaSoft.MvvmLight.CommandWpf;
    using GalaSoft.MvvmLight.Messaging;
    using GalaSoft.MvvmLight.Threading;
    using MediaElementAdaption;
    using Models;
    using PubSubMessages;
    using Serilog;
    using Services.FrozenVideoItems;
    using Services.HiddenMediaItems;
    using Services.MediaChanging;
    using Services.MetaDataQueue;
    using Services.Pages;

    // ReSharper disable once ClassNeverInstantiated.Global
    internal sealed class OperatorViewModel : ViewModelBase, IDisposable
    {
        private readonly IMediaProviderService _mediaProviderService;
        private readonly IThumbnailService _thumbnailService;
        private readonly IMediaMetaDataService _metaDataService;
        private readonly IOptionsService _optionsService;
        private readonly IPageService _pageService;
        private readonly IMediaStatusChangingService _mediaStatusChangingService;
        private readonly IHiddenMediaItemsService _hiddenMediaItemsService;
        private readonly IFrozenVideosService _frozenVideosService;
        private readonly IActiveMediaItemsService _activeMediaItemsService;

        private readonly MetaDataQueueProducer _metaDataProducer = new MetaDataQueueProducer();
        private readonly CancellationTokenSource _metaDataCancellationTokenSource = new CancellationTokenSource();
        
        private MetaDataQueueConsumer _metaDataConsumer;
        private string _blankScreenImagePath;
        private bool _pendingLoadMediaItems;

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
            IFrozenVideosService frozenVideosService)
        {
            _mediaProviderService = mediaProviderService;
            _mediaStatusChangingService = mediaStatusChangingService;

            _hiddenMediaItemsService = hiddenMediaItemsService;
            _hiddenMediaItemsService.UnhideAllEvent += HandleUnhideAllEvent;

            _activeMediaItemsService = activeMediaItemsService;
            _frozenVideosService = frozenVideosService;

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
            _optionsService.ShowFreezeCommandChangedEvent += HandleShowFreezeCommandChangedEvent;
            _optionsService.OperatingDateChangedEvent += HandleOperatingDateChangedEvent;
            _optionsService.MaxItemCountChangedEvent += HandleMaxItemCountChangedEvent;
            _optionsService.PermanentBackdropChangedEvent += async (sender, e) =>
            {
                await HandlePermanentBackdropChangedEvent(sender, e);
            };
            _optionsService.IncludeBlankScreenItemChangedEvent += async (sender, e) =>
            {
                await HandleIncludeBlankScreenItemChangedEvent(sender, e);
            };

            folderWatcherService.ChangesFoundEvent += HandleFileChangesFoundEvent;

            _pageService = pageService;
            _pageService.MediaChangeEvent += HandleMediaChangeEvent;
            _pageService.MediaMonitorChangedEvent += HandleMediaMonitorChangedEvent;
            _pageService.MediaPositionChangedEvent += HandleMediaPositionChangedEvent;
            _pageService.MediaNearEndEvent += async (sender, e) =>
            {
                await HandleMediaNearEndEvent(sender, e);
            };
            _pageService.NavigationEvent += HandleNavigationEvent;

            LoadMediaItems();
            InitCommands();

            LaunchThumbnailQueueConsumer();

            Messenger.Default.Register<ShutDownMessage>(this, OnShutDown);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "_thumbnailCancellationTokenSource", Justification = "False Positive")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "_metaDataProducer", Justification = "False Positive")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "_metaDataConsumer", Justification = "False Positive")]
        public void Dispose()
        {
            _metaDataProducer?.Dispose();
            _metaDataCancellationTokenSource?.Dispose();
            _metaDataConsumer?.Dispose();
        }

        private void HandleMaxItemCountChangedEvent(object sender, EventArgs e)
        {
            _pendingLoadMediaItems = true;
        }

        private void HandleNavigationEvent(object sender, NavigationEventArgs e)
        {
            if (e.PageName.Equals(_pageService.OperatorPageName))
            {
                if (_pendingLoadMediaItems)
                {
                    _pendingLoadMediaItems = false;
                    LoadMediaItems();
                }
            }
        }

        private void HandleOperatingDateChangedEvent(object sender, EventArgs e)
        {
            _pendingLoadMediaItems = true;
        }

        private void HandleUnhideAllEvent(object sender, EventArgs e)
        {
            foreach (var item in MediaItems)
            {
                item.IsVisible = true;
            }
        }

        private void HandleShowMediaItemCommandPanelChangedEvent(object sender, EventArgs e)
        {
            var visible = _optionsService.Options.ShowMediaItemCommandPanel;

            foreach (var item in MediaItems)
            {
                item.CommandPanelVisible = visible;
            }
        }

        private void HandleShowFreezeCommandChangedEvent(object sender, EventArgs e)
        {
            var allow = _optionsService.Options.ShowFreezeCommand;

            foreach (var item in MediaItems)
            {
                item.AllowFreezeCommand = allow;
            }
        }

        private async Task HandleIncludeBlankScreenItemChangedEvent(object sender, EventArgs e)
        {
            if (!_optionsService.Options.IncludeBlanksScreenItem)
            {
                await RemoveBlankScreenItem();
            }

            _pendingLoadMediaItems = true;
        }

        private async Task HandlePermanentBackdropChangedEvent(object sender, EventArgs e)
        {
            if (_optionsService.Options.PermanentBackdrop)
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

        private MediaItem GetActiveBlankScreenItem()
        {
            var items = GetCurrentMediaItems();
            return items?.SingleOrDefault(x => x.IsBlankScreen && x.IsMediaActive);
        }

        private void HandleUseInternalMediaTitlesChangedEvent(object sender, EventArgs e)
        {
            foreach (var item in MediaItems)
            {
                item.Name = null;
            }

            FillThumbnailsAndMetaData();
            
            _pendingLoadMediaItems = true;
        }

        private void HandleAllowVideoPositionSeekingChangedEvent(object sender, EventArgs e)
        {
            foreach (var item in MediaItems)
            {
                item.AllowPositionSeeking = _optionsService.Options.AllowVideoPositionSeeking;
            }
        }

        private void HandleAllowVideoPauseChangedEvent(object sender, EventArgs e)
        {
            foreach (var item in MediaItems)
            {
                item.AllowPause = _optionsService.Options.AllowVideoPause;
            }
        }

        private async Task HandleMediaNearEndEvent(object sender, MediaNearEndEventArgs e)
        {
            var item = GetMediaItem(e.MediaItemId);
            if (item != null)
            {
                if (item.PauseOnLastFrame)
                {
                    await MediaPauseControl(item.Id);
                }
            }
        }

        private void HandleMediaPositionChangedEvent(object sender, PositionChangedEventArgs e)
        {
            var item = GetMediaItem(e.MediaItemId);
            if (item != null)
            {
                item.PlaybackPositionDeciseconds = (int)(e.Position.TotalMilliseconds / 100);
            }
        }

        private void OnShutDown(ShutDownMessage message)
        {
            // cancel the thumbnail consumer thread.
            _metaDataCancellationTokenSource.Cancel();
        }

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

        private void HandleItemCompletedEvent(object sender, ItemMetaDataPopulatedEventArgs e)
        {
            var item = e.MediaItem;
            
            if (_optionsService.Options.AutoRotateImages)
            {
                AutoRotateImageIfRequired(item);
            }
        }

        private void HandleFileChangesFoundEvent(object sender, EventArgs e)
        {
            DispatcherHelper.CheckBeginInvokeOnUI(LoadMediaItems);
        }

        private void HandleMediaMonitorChangedEvent(object sender, EventArgs e)
        {
            ChangePlayButtonEnabledStatus();
        }

        private void ChangePlayButtonEnabledStatus()
        {
            var monitorSpecified = _optionsService.IsMediaMonitorSpecified;
            var videoOrAudioIsActive = VideoOrAudioIsActive();
            var videoIsActive = VideoIsActive();

            foreach (var item in MediaItems)
            {
                switch (item.MediaType.Classification)
                {
                    case MediaClassification.Image:
                        // cannot show an image if video or audio is playing.
                        item.IsPlayButtonEnabled = monitorSpecified && !videoIsActive;
                        break;

                    case MediaClassification.Audio:
                        // cannot start audio if another video or audio is playing.
                        item.IsPlayButtonEnabled = !videoOrAudioIsActive;
                        break;

                    case MediaClassification.Video:
                        // cannot play a video if another video or audio is playing.
                        item.IsPlayButtonEnabled = monitorSpecified && !videoOrAudioIsActive;
                        break;

                    default:
                        item.IsPlayButtonEnabled = false;
                        break;
                }
            }
        }

        private void HandleMediaChangeEvent(object sender, MediaEventArgs e)
        {
            var mediaItem = GetMediaItem(e.MediaItemId);
            if (mediaItem == null)
            {
                return;
            }

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
            MediaControlCommand1 = new RelayCommand<Guid>(async (mediaItemId) =>
            {
                await MediaControl1(mediaItemId);
            });

            MediaControlPauseCommand = new RelayCommand<Guid>(async (mediaItemId) =>
            {
                await MediaPauseControl(mediaItemId);
            });

            HideMediaItemCommand = new RelayCommand<Guid>(HideMediaItem);

            DeleteMediaItemCommand = new RelayCommand<Guid>(DeleteMediaItem);

            OpenCommandPanelCommand = new RelayCommand<Guid>(OpenCommandPanel);

            CloseCommandPanelCommand = new RelayCommand<Guid>(CloseCommandPanel);

            FreezeVideoCommand = new RelayCommand<Guid>(FreezeVideoOnLastFrame);
        }

        private void FreezeVideoOnLastFrame(Guid mediaItemId)
        {
            var mediaItem = GetMediaItem(mediaItemId);
            if (mediaItem != null)
            {
                Debug.Assert(
                    mediaItem.MediaType.Classification == MediaClassification.Video, 
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
        }

        private void CloseCommandPanel(Guid mediaItemId)
        {
            var mediaItem = GetMediaItem(mediaItemId);
            if (mediaItem != null)
            {
                mediaItem.IsCommandPanelOpen = false;
            }
        }

        private void OpenCommandPanel(Guid mediaItemId)
        {
            var mediaItem = GetMediaItem(mediaItemId);
            if (mediaItem != null)
            {
                mediaItem.IsCommandPanelOpen = true;
            }
        }

        private void HideMediaItem(Guid mediaItemId)
        {
            var mediaItem = GetMediaItem(mediaItemId);
            if (mediaItem != null)
            {
                mediaItem.IsCommandPanelOpen = false;

                Task.Delay(400).ContinueWith(t =>
                {
                    mediaItem.IsVisible = false;
                    _hiddenMediaItemsService.Add(mediaItem.FilePath);
                });
            }
        }

        private void DeleteMediaItem(Guid mediaItemId)
        {
            var mediaItem = GetMediaItem(mediaItemId);
            if (mediaItem != null)
            {
                if (FileUtils.SafeDeleteFile(mediaItem.FilePath))
                {
                    mediaItem.IsCommandPanelOpen = false;
                }
            }
        }

        private bool IsVideoOrAudio(MediaItem mediaItem)
        {
            return
                mediaItem.MediaType.Classification == MediaClassification.Audio ||
                mediaItem.MediaType.Classification == MediaClassification.Video;
        }

        private bool IsVideo(MediaItem mediaItem)
        {
            return mediaItem.MediaType.Classification == MediaClassification.Video;
        }

        private async Task MediaPauseControl(Guid mediaItemId)
        {
            // only allow pause media when nothing is changing.
            if (!_mediaStatusChangingService.IsMediaStatusChanging())
            {
                var mediaItem = GetMediaItem(mediaItemId);
                if (mediaItem == null || !IsVideoOrAudio(mediaItem))
                {
                    Log.Error($"Media Item not found (id = {mediaItemId})");
                    return;
                }

                if (mediaItem.IsMediaActive)
                {
                    if (mediaItem.IsPaused)
                    {
                        await _pageService.StartMedia(mediaItem, GetCurrentMediaItems(), true);
                    }
                    else
                    {
                        await _pageService.PauseMediaAsync(mediaItem);
                    }
                }
            }
        }

        private async Task MediaControl1(Guid mediaItemId)
        {
            // only allow start/stop media when nothing is changing.
            if (!_mediaStatusChangingService.IsMediaStatusChanging())
            {
                var mediaItem = GetMediaItem(mediaItemId);
                if (mediaItem == null)
                {
                    Log.Error($"Media Item not found (id = {mediaItemId})");
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
            switch (item.MediaType.Classification)
            {
                case MediaClassification.Audio:
                    return !VideoOrAudioIsActive();

                case MediaClassification.Image:
                    return !VideoIsActive();

                case MediaClassification.Video:
                    return !VideoOrAudioIsActive();

                default:
                case MediaClassification.Unknown:
                    return false;
            }
        }

        private IReadOnlyCollection<MediaItem> GetCurrentMediaItems()
        {
            if (!_activeMediaItemsService.Any())
            {
                return null;
            }

            return _activeMediaItemsService.GetMediaItemIds().Select(GetMediaItem).ToList();
        }
        
        private bool VideoOrAudioIsActive()
        {
            var currentItems = GetCurrentMediaItems();
            if (currentItems == null)
            {
                return false;
            }

            return currentItems.Any(IsVideoOrAudio);
        }

        private bool VideoIsActive()
        {
            var currentItems = GetCurrentMediaItems();
            if (currentItems == null)
            {
                return false;
            }

            return currentItems.Any(IsVideo);
        }

        private MediaItem GetNextImageItem(MediaItem currentMediaItem)
        {
            if (currentMediaItem == null)
            {
                return null;
            }

            var found = false;
            foreach (var item in MediaItems)
            {
                if (found && item.MediaType.Classification == MediaClassification.Image)
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

        private MediaItem GetMediaItem(Guid mediaItemId)
        {
            return MediaItems.SingleOrDefault(x => x.Id == mediaItemId);
        }

        private void HandleMediaFolderChangedEvent(object sender, EventArgs e)
        {
            _pendingLoadMediaItems = true;
        }

        private void HandleThumbnailsPurgedEvent(object sender, EventArgs e)
        {
            foreach (var item in MediaItems)
            {
                item.ThumbnailImageSource = null;
            }

            FillThumbnailsAndMetaData();
        }

        private void LoadMediaItems()
        {
            if (IsInDesignMode)
            {
                return;
            }

            Log.Logger.Debug("Loading media items");
            
            Messenger.Default.Send(new MediaListUpdatingMessage());

            using (new ObservableCollectionSupression<MediaItem>(MediaItems))
            {
                LoadMediaItemsInternal();
            }

            ChangePlayButtonEnabledStatus();

            Messenger.Default.Send(new MediaListUpdatedMessage { Count = MediaItems.Count });
            
            Log.Logger.Debug("Completed loading media items");
        }

        private void LoadMediaItemsInternal()
        {
            var files = _mediaProviderService.GetMediaFiles();
            var sourceFilePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var destFilePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in files)
            {
                sourceFilePaths.Add(file.FullPath);
            }

            var itemsToRemove = new List<MediaItem>();

            foreach (var item in MediaItems)
            {
                if (!sourceFilePaths.Contains(item.FilePath))
                {
                    // remove this item.
                    itemsToRemove.Add(item);
                }

                destFilePaths.Add(item.FilePath);
            }

            // remove old items.
            foreach (var item in itemsToRemove)
            {
                MediaItems.Remove(item);
            }

            // add new items.
            foreach (var file in files)
            {
                if (!destFilePaths.Contains(file.FullPath))
                {
                    var item = CreateNewMediaItem(file);

                    MediaItems.Add(item);

                    _metaDataProducer.Add(item);
                }
            }

            SortMediaItems();

            TruncateMediaItemsToMaxCount();
            
            _hiddenMediaItemsService.Init(MediaItems);
            _frozenVideosService.Init(MediaItems);

            InsertBlankMediaItem();
        }

        private void TruncateMediaItemsToMaxCount()
        {
            while (MediaItems.Count > _optionsService.Options.MaxItemCount)
            {
                MediaItems.RemoveAt(MediaItems.Count - 1);
            }
        }

        private MediaItem CreateNewMediaItem(MediaFile file)
        {
            return new MediaItem
            {
                MediaType = file.MediaType,
                Id = Guid.NewGuid(),
                AllowFreezeCommand = _optionsService.Options.ShowFreezeCommand,
                CommandPanelVisible = _optionsService.Options.ShowMediaItemCommandPanel,
                FilePath = file.FullPath,
                IsVisible = true,
                LastChanged = file.LastChanged,
                AllowPause = _optionsService.Options.AllowVideoPause,
                AllowPositionSeeking = _optionsService.Options.AllowVideoPositionSeeking
            };
        }

        private void InsertBlankMediaItem()
        {
            // only add a "blank screen" item if we don't already display
            // a permanent black backdrop.
            if (!_optionsService.Options.PermanentBackdrop && _optionsService.Options.IncludeBlanksScreenItem)
            {
                var blankScreenPath = GetBlankScreenPath();

                var item = new MediaItem
                {
                    MediaType = _mediaProviderService.GetSupportedMediaType(blankScreenPath),
                    Id = Guid.NewGuid(),
                    IsBlankScreen = true,
                    IsVisible = true,
                    AllowFreezeCommand = _optionsService.Options.ShowFreezeCommand,
                    CommandPanelVisible = _optionsService.Options.ShowMediaItemCommandPanel,
                    Name = Properties.Resources.BLANK_SCREEN,
                    FilePath = blankScreenPath,
                    LastChanged = DateTime.UtcNow.Ticks
                };

                MediaItems.Insert(0, item);

                _metaDataProducer.Add(item);
            }
        }

        private string GetBlankScreenPath()
        {
            if (_blankScreenImagePath == null)
            {
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
                    Log.Logger.Error(ex, "Could not save blank screen image");
                }
            }

            return _blankScreenImagePath;
        }
       
        private void SortMediaItems()
        {
            List<MediaItem> sorted = MediaItems.OrderBy(x => x.AlphaNumericName).ToList();

            for (int n = 0; n < sorted.Count; ++n)
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

        public ObservableCollectionEx<MediaItem> MediaItems { get; } = new ObservableCollectionEx<MediaItem>();

        public RelayCommand<Guid> MediaControlCommand1 { get; set; }

        public RelayCommand<Guid> MediaControlPauseCommand { get; set; }

        public RelayCommand<Guid> HideMediaItemCommand { get; set; }

        public RelayCommand<Guid> DeleteMediaItemCommand { get; set; }

        public RelayCommand<Guid> OpenCommandPanelCommand { get; set; }

        public RelayCommand<Guid> CloseCommandPanelCommand { get; set; }

        public RelayCommand<Guid> FreezeVideoCommand { get; set; }

        private bool AutoRotateImageIfRequired(MediaItem item)
        {
            if (item.MediaType.Classification == MediaClassification.Image)
            {
                if (GraphicsUtils.AutoRotateIfRequired(item.FilePath))
                {
                    // auto-rotated so refresh the thumbnail...
                    item.ThumbnailImageSource = null;
                    item.LastChanged = DateTime.UtcNow.Ticks;
                    _metaDataProducer.Add(item);

                    return true;
                }
            }

            return false;
        }

        private void HandleAutoRotateChangedEvent(object sender, EventArgs e)
        {
            Task.Run(() =>
            {
                if (_optionsService.Options.AutoRotateImages)
                {
                    foreach (var item in MediaItems)
                    {
                        AutoRotateImageIfRequired(item);
                    }
                }

                _pendingLoadMediaItems = true;
            });
        }
    }
}
