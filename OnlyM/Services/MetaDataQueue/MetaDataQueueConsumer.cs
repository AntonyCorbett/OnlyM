namespace OnlyM.Services.MetaDataQueue
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Core.Models;
    using Core.Services.Media;
    using Core.Services.Options;
    using Core.Utils;
    using GalaSoft.MvvmLight.Threading;
    using Models;
    using Serilog;

    internal sealed class MetaDataQueueConsumer : IDisposable
    {
        private readonly IThumbnailService _thumbnailService;
        private readonly IMediaMetaDataService _metaDataService;
        private readonly IOptionsService _optionsService;

        private readonly BlockingCollection<MediaItem> _collection;
        private readonly CancellationToken _cancellationToken;

        public event EventHandler<ItemMetaDataPopulatedEventArgs> ItemCompletedEvent;

        public MetaDataQueueConsumer(
            IThumbnailService thumbnailService,
            IMediaMetaDataService metaDataService,
            IOptionsService optionsService,
            BlockingCollection<MediaItem> metaDataProducerCollection,
            CancellationToken cancellationToken)
        {
            _thumbnailService = thumbnailService;
            _metaDataService = metaDataService;
            _optionsService = optionsService;

            _collection = metaDataProducerCollection;
            _cancellationToken = cancellationToken;
        }

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
            Task.Run(
                () =>
                {
                    try
                    {
                        while (!_cancellationToken.IsCancellationRequested)
                        {
                            var nextItem = _collection.Take(_cancellationToken);

                            Log.Logger.Debug($"Consuming item {nextItem.FilePath}");
                            PopulateThumbnailAndMetaData(nextItem);

                            if (!IsPopulated(nextItem))
                            {
                                // put it back in the queue!
                                ReplaceInQueue(nextItem);
                            }
                            else
                            {
                                Log.Logger.Debug($"Done item {nextItem.FilePath}");
                                ItemCompletedEvent?.Invoke(this, new ItemMetaDataPopulatedEventArgs { MediaItem = nextItem });
                            }

                            Log.Logger.Verbose("Metadata queue size (consumer) = {QueueSize}", _collection.Count);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        Log.Logger.Debug("Metadata consumer closed");
                    }
                },
                _cancellationToken);
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
            PopulateThumbnail(mediaItem);
            PopulateDurationAndName(mediaItem);
        }

        private bool IsPopulated(MediaItem mediaItem)
        {
            return IsThumbnailPopulated(mediaItem) &&
                   IsDurationAndNamePopulated(mediaItem);
        }

        private bool IsThumbnailPopulated(MediaItem mediaItem)
        {
            return mediaItem.ThumbnailImageSource != null;
        }

        private bool IsDurationAndNamePopulated(MediaItem mediaItem)
        {
            return 
                (!mediaItem.HasDuration || mediaItem.DurationDeciseconds > 0) &&
                !string.IsNullOrEmpty(mediaItem.Name);
        }

        private void PopulateDurationAndName(MediaItem mediaItem)
        {
            if (!IsDurationAndNamePopulated(mediaItem))
            {
                var metaData = _metaDataService.GetMetaData(mediaItem.FilePath);
                if (metaData != null)
                {
                    DispatcherHelper.CheckBeginInvokeOnUI(() =>
                    {
                        mediaItem.DurationDeciseconds = (int)(metaData.Duration.TotalSeconds * 10);
                        mediaItem.Name = GetMediaTitle(mediaItem.FilePath, metaData);
                    });
                }
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
                        mediaItem.ThumbnailImageSource = GraphicsUtils.ByteArrayToImage(thumb);
                    });
                }
            }
        }

        private string GetMediaTitle(string filePath, MediaMetaData metaData)
        {
            if (_optionsService.Options.UseInternalMediaTitles && metaData != null)
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
