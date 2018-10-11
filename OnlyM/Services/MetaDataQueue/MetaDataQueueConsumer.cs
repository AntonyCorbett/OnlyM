﻿namespace OnlyM.Services.MetaDataQueue
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
            Task.Run(() => { RunConsumerTask(); }, _cancellationToken);
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
                            Log.Logger.Debug($"Done item {nextItem.FilePath}");
                            ItemCompletedEvent?.Invoke(this, new ItemMetaDataPopulatedEventArgs { MediaItem = nextItem });
                        }

                        Log.Logger.Verbose("Metadata queue size (consumer) = {QueueSize}", _collection.Count);

                        if (_collection.Count == 0)
                        {
                            AllItemsCompletedEvent?.Invoke(this, EventArgs.Empty);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Log.Logger.Debug("Metadata consumer closed");
            }
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
            PopulateSlideData(mediaItem);
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
                   IsDurationAndNamePopulated(mediaItem) &&
                   IsSlideDataPopulated(mediaItem);
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

        private bool IsSlideDataPopulated(MediaItem mediaItem)
        {
            return !mediaItem.IsSlideshow || mediaItem.SlideshowCount > 0;
        }

        private void PopulateDurationAndName(MediaItem mediaItem)
        {
            if (!IsDurationAndNamePopulated(mediaItem))
            {
                var metaData = _metaDataService.GetMetaData(
                    mediaItem.FilePath, mediaItem.MediaType, _ffmpegFolder);

                DispatcherHelper.CheckBeginInvokeOnUI(() =>
                {
                    mediaItem.DurationDeciseconds = metaData == null ? 0 : (int)(metaData.Duration.TotalSeconds * 10);
                    mediaItem.Name = GetMediaTitle(mediaItem.FilePath, metaData);
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
