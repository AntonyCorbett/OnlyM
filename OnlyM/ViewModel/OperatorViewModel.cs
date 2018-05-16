namespace OnlyM.ViewModel
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Core.Models;
    using Core.Services.Media;
    using Core.Services.Options;
    using GalaSoft.MvvmLight;
    using GalaSoft.MvvmLight.Command;
    using GalaSoft.MvvmLight.Messaging;
    using GalaSoft.MvvmLight.Threading;
    using Models;
    using PubSubMessages;
    using Serilog;
    using Services.MetaDataQueue;
    using Services.Pages;

    // ReSharper disable once ClassNeverInstantiated.Global
    internal class OperatorViewModel : ViewModelBase
    {
        private readonly IMediaProviderService _mediaProviderService;
        private readonly IThumbnailService _thumbnailService;
        private readonly IMediaMetaDataService _metaDataService;
        private readonly IOptionsService _optionsService;
        private readonly IPageService _pageService;

        private readonly HashSet<Guid> _changingMediaItems = new HashSet<Guid>();
        private readonly MetaDataQueueProducer _metaDataProducer = new MetaDataQueueProducer();
        private readonly CancellationTokenSource _thumbnailCancellationTokenSource = new CancellationTokenSource();

        private MetaDataQueueConsumer _metaDataConsumer;

        public OperatorViewModel(
            IMediaProviderService mediaProviderService,
            IThumbnailService thumbnailService,
            IOptionsService optionsService,
            IPageService pageService,
            IFolderWatcherService folderWatcherService,
            IMediaMetaDataService metaDataService)
        {
            _mediaProviderService = mediaProviderService;

            _thumbnailService = thumbnailService;
            _thumbnailService.ThumbnailsPurgedEvent += HandleThumbnailsPurgedEvent;

            _optionsService = optionsService;
            _optionsService.MediaFolderChangedEvent += HandleMediaFolderChangedEvent;
            
            folderWatcherService.ChangesFoundEvent += HandleFileChangesFoundEvent;

            _pageService = pageService;
            _pageService.MediaChangeEvent += HandleMediaChangeEvent;
            _pageService.MediaMonitorChangedEvent += HandleMediaMonitorChangedEvent;
            _pageService.MediaPositionChangedEvent += HandleMediaPositionChangedEvent;

            _metaDataService = metaDataService;

            LoadMediaItems();
            InitCommands();

            LaunchThumbnailQueueConsumer();

            Messenger.Default.Register<ShutDownMessage>(this, OnShutDown);
        }

        private void HandleMediaPositionChangedEvent(object sender, Unosquare.FFME.Events.PositionChangedRoutedEventArgs e)
        {
            var item = GetCurrentMediaItem();
            if (item != null)
            {
                item.PlaybackPositionDeciseconds = (int)(e.Position.TotalMilliseconds / 10);
            }
        }

        private void OnShutDown(ShutDownMessage message)
        {
            // cancel the thumbnail consumer thread.
            _thumbnailCancellationTokenSource.Cancel();
        }

        private void LaunchThumbnailQueueConsumer()
        {
            _metaDataConsumer = new MetaDataQueueConsumer(
                _thumbnailService,
                _metaDataService,
                _metaDataProducer.Queue,
                _thumbnailCancellationTokenSource.Token);

            _metaDataConsumer.Execute();
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
            
            foreach (var item in MediaItems)
            {
                switch (item.MediaType.Classification)
                {
                    case MediaClassification.Image:
                        // cannot show an image if video or audio is playing.
                        item.IsPlayButtonEnabled = monitorSpecified && !videoOrAudioIsActive;
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
                    _changingMediaItems.Add(mediaItem.Id);
                    break;

                case MediaChange.Stopping:
                    mediaItem.IsMediaActive = false;
                    mediaItem.IsMediaChanging = true;
                    _changingMediaItems.Add(mediaItem.Id);
                    break;

                case MediaChange.Started:
                    mediaItem.IsMediaActive = true;
                    mediaItem.IsMediaChanging = false;
                    _changingMediaItems.Remove(mediaItem.Id);
                    break;

                case MediaChange.Stopped:
                    mediaItem.IsMediaActive = false;
                    mediaItem.IsMediaChanging = false;
                    mediaItem.PlaybackPositionDeciseconds = 0;
                    _changingMediaItems.Remove(mediaItem.Id);
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
        }

        private async Task MediaControl1(Guid mediaItemId)
        {
            // only allow start/stop media when nothing is changing.
            if (_changingMediaItems.Count == 0)
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
                    // prevent start media if a video is active (videos must be stopped manually first).
                    if (!VideoOrAudioIsActive())
                    {
                        _pageService.StartMedia(mediaItem, GetCurrentMediaItem());

                        // when displaying an item we ensure that the next image item is cached.
                        _pageService.CacheImageItem(GetNextImageItem(mediaItem));
                    }
                }
            }
        }

        private MediaItem GetCurrentMediaItem()
        {
            if (_pageService.CurrentMediaId == Guid.Empty)
            {
                return null;
            }

            return GetMediaItem(_pageService.CurrentMediaId);
        }
        
        private bool VideoOrAudioIsActive()
        {
            var currentItem = GetCurrentMediaItem();
            if (currentItem == null)
            {
                return false;
            }
                
            return 
                currentItem.MediaType.Classification == MediaClassification.Video ||
                currentItem.MediaType.Classification == MediaClassification.Audio;
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
            LoadMediaItems();
        }

        private void HandleThumbnailsPurgedEvent(object sender, EventArgs e)
        {
            foreach (var item in MediaItems)
            {
                item.ThumbnailImageSource = null;
            }

            FillThumbnails();
        }

        private void LoadMediaItems()
        {
            if (IsInDesignMode)
            {
                return;
            }

            var itemsToRemove = new List<MediaItem>();

            IReadOnlyCollection<MediaFile> files = _mediaProviderService.GetMediaFiles();
            var sourceFilePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var destFilePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in files)
            {
                sourceFilePaths.Add(file.FullPath);
            }

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
                    var item = new MediaItem
                    {
                        MediaType = file.MediaType,
                        Id = Guid.NewGuid(),
                        Name = Path.GetFileNameWithoutExtension(file.FullPath),
                        FilePath = file.FullPath,
                        LastChanged = file.LastChanged
                    };

                    MediaItems.Add(item);

                    _metaDataProducer.Add(item);
                }
            }

            SortMediaItems();
            
            ChangePlayButtonEnabledStatus();
        }

        private void SortMediaItems()
        {
            List<MediaItem> sorted = MediaItems.OrderBy(x => x.Name).ToList();

            for (int n = 0; n < sorted.Count; ++n)
            {
                MediaItems.Move(MediaItems.IndexOf(sorted[n]), n);
            }
        }

        private void FillThumbnails()
        {
            foreach (var item in MediaItems)
            {
                _metaDataProducer.Add(item);
            }
        }

        public ObservableCollection<MediaItem> MediaItems { get; } = new ObservableCollection<MediaItem>();

        public RelayCommand<Guid> MediaControlCommand1 { get; set; }
    }
}
