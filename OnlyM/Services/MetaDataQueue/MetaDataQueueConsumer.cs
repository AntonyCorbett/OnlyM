namespace OnlyM.Services.MetaDataQueue
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using GalaSoft.MvvmLight.Threading;
    using OnlyM.Core.Models;
    using OnlyM.Core.Services.Media;
    using OnlyM.Core.Services.Options;
    using OnlyM.CoreSys;
    using OnlyM.Models;
    using OnlyM.Slides;
    using Serilog;

    internal sealed class MetaDataQueueConsumer : IDisposable
    {
        private readonly IThumbnailService _thumbnailService;
        private readonly IMediaMetaDataService _metaDataService;
        private readonly IOptionsService _optionsService;

        private readonly BlockingCollection<MediaItem> _collection;
        private readonly CancellationToken _cancellationToken;
        private readonly string _ffmpegFolder;

        public MetaDataQueueConsumer(
            IThumbnailService thumbnailService,
            IMediaMetaDataService metaDataService,
            IOptionsService optionsService,
            BlockingCollection<MediaItem> metaDataProducerCollection,
            string ffmpegFolder,
            CancellationToken cancellationToken)
        {
            _thumbnailService = thumbnailService;
            _metaDataService = metaDataService;
            _optionsService = optionsService;
            
            _ffmpegFolder = ffmpegFolder;

            _collection = metaDataProducerCollection;
            _cancellationToken = cancellationToken;
        }

        public event EventHandler<ItemMetaDataPopulatedEventArgs> ItemCompletedEvent;

        public event EventHandler AllItemsCompletedEvent;

        public void Execute()
        {
            RunConsumer();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "_problemFiles", Justification = "False Positive")]
        public void Dispose()
        {
            _collection?.Dispose();
        }

        private void RunConsumer()
        {
            Task.Run(RunConsumerTask, _cancellationToken);
        }

        private void RunConsumerTask()
        {
            try
            {
                while (!_cancellationToken.IsCancellationRequested)
                {
                    var nextItem = _collection.Take(_cancellationToken);

                    Log.Logger.Debug($"Consuming item {nextItem.FilePath}");

                    if (!IsPopulated(nextItem))
                    {
                        PopulateThumbnailAndMetaData(nextItem);

                        if (!IsPopulated(nextItem))
                        {
                            // put it back in the queue!
                            ReplaceInQueue(nextItem);
                        }
                        else
                        {
                            ItemCompleted(nextItem);
                        }

                        Log.Logger.Verbose("Metadata queue size (consumer) = {QueueSize}", _collection.Count);

                        if (_collection.Count == 0)
                        {
                            AllItemsCompletedEvent?.Invoke(this, EventArgs.Empty);
                        }
                    }
                    else
                    {
                        ItemCompleted(nextItem);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Log.Logger.Debug("Metadata consumer closed");
            }
        }

        private void ItemCompleted(MediaItem nextItem)
        {
            Log.Logger.Debug($"Done item {nextItem.FilePath}");
            ItemCompletedEvent?.Invoke(this, new ItemMetaDataPopulatedEventArgs { MediaItem = nextItem });
        }

        private void ReplaceInQueue(MediaItem mediaItem)
        {
            Task.Delay(2000, _cancellationToken)
                .ContinueWith(
                    t =>
                    {
                        Log.Logger.Debug($"Replaced in queue {mediaItem.FilePath}");
                        _collection.Add(mediaItem, _cancellationToken);
                    }, 
                    _cancellationToken);
        }

        private void PopulateThumbnailAndMetaData(MediaItem mediaItem)
        {
            PopulateSlideData(mediaItem);
            PopulateThumbnail(mediaItem);
            PopulateDurationAndTitle(mediaItem);
        }

        private void PopulateSlideData(MediaItem mediaItem)
        {
            if (!IsSlideDataPopulated(mediaItem))
            {
                var sf = new SlideFile(mediaItem.FilePath);
                mediaItem.SlideshowCount = sf.SlideCount;
                mediaItem.SlideshowLoop = sf.Loop;
                mediaItem.IsRollingSlideshow = sf.AutoPlay;
            }
        }

        private bool IsPopulated(MediaItem mediaItem)
        {
            return IsThumbnailPopulated(mediaItem) &&
                   IsDurationAndTitlePopulated(mediaItem) &&
                   IsSlideDataPopulated(mediaItem);
        }

        private bool IsThumbnailPopulated(MediaItem mediaItem)
        {
            return mediaItem.ThumbnailImageSource != null;
        }

        private bool IsDurationAndTitlePopulated(MediaItem mediaItem)
        {
            return 
                (!mediaItem.HasDuration || mediaItem.DurationDeciseconds > 0) &&
                !string.IsNullOrEmpty(mediaItem.Title);
        }

        private bool IsSlideDataPopulated(MediaItem mediaItem)
        {
            return !mediaItem.IsSlideshow || mediaItem.SlideshowCount > 0;
        }

        private void PopulateDurationAndTitle(MediaItem mediaItem)
        {
            if (!IsDurationAndTitlePopulated(mediaItem))
            {
                var metaData = _metaDataService.GetMetaData(
                    mediaItem.FilePath, mediaItem.MediaType, _ffmpegFolder);

                DispatcherHelper.CheckBeginInvokeOnUI(() =>
                {
                    if (!IsDurationAndTitlePopulated(mediaItem))
                    {
                        mediaItem.DurationDeciseconds =
                            metaData == null ? 0 : (int)(metaData.Duration.TotalSeconds * 10);
                        mediaItem.Title = GetMediaTitle(mediaItem.FilePath, metaData);
                        mediaItem.FileNameAsSubTitle = _optionsService.UseInternalMediaTitles
                            ? Path.GetFileName(mediaItem.FilePath)
                            : null;
                    }
                });
            }
        }

        private void PopulateThumbnail(MediaItem mediaItem)
        {
            if (!IsThumbnailPopulated(mediaItem))
            {
                // ReSharper disable once StyleCop.SA1117
                byte[] thumb = _thumbnailService.GetThumbnail(
                    mediaItem.FilePath,
                    Unosquare.FFME.MediaElement.FFmpegDirectory,
                    mediaItem.MediaType.Classification,
                    mediaItem.LastChanged,
                    out var _);

                if (thumb != null)
                {
                    DispatcherHelper.CheckBeginInvokeOnUI(() =>
                    {
                        if (!IsThumbnailPopulated(mediaItem))
                        {
                            mediaItem.ThumbnailImageSource = GraphicsUtils.ByteArrayToImage(thumb);
                        }
                    });
                }
            }
        }

        private string GetMediaTitle(string filePath, MediaMetaData metaData)
        {
            if (_optionsService.UseInternalMediaTitles && metaData != null)
            {
                if (!string.IsNullOrEmpty(metaData.Title))
                {
                    return metaData.Title;
                }
            }

            return Path.GetFileNameWithoutExtension(filePath);
        }
    }
}
