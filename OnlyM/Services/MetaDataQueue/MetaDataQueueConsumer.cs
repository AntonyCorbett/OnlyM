using OnlyM.Core.Models;

namespace OnlyM.Services.MetaDataQueue
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;
    using Core.Services.Media;
    using Core.Utils;
    using GalaSoft.MvvmLight.Threading;
    using Models;
    using Serilog;

    internal class MetaDataQueueConsumer
    {
        private readonly IThumbnailService _thumbnailService;
        private readonly IMediaMetaDataService _metaDataService;
        private readonly BlockingCollection<MediaItem> _collection;
        private readonly CancellationToken _cancellationToken;

        public MetaDataQueueConsumer(
            IThumbnailService thumbnailService,
            IMediaMetaDataService metaDataService,
            BlockingCollection<MediaItem> metaDataProducerCollection,
            CancellationToken cancellationToken)
        {
            _thumbnailService = thumbnailService;
            _metaDataService = metaDataService;

            _collection = metaDataProducerCollection;
            _cancellationToken = cancellationToken;
        }

        public void Execute()
        {
            Task.Run(
                () =>
                {
                    try
                    {
                        while (!_cancellationToken.IsCancellationRequested)
                        {
                            var nextItem = _collection.Take(_cancellationToken);
                            PopulateThumbnail(nextItem);
                            PopulateMetaData(nextItem);

                            Log.Logger.Verbose("Metadata queue size (consumer) = {QueueSize}", _collection.Count);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        Log.Logger.Information("Metadata consumer closed");
                    }
                }, 
                _cancellationToken);
        }

        private void PopulateMetaData(MediaItem mediaItem)
        {
            var md = _metaDataService.GetMetaData(mediaItem.FilePath, mediaItem.MediaType.Classification);
            if (md != null)
            {
                DispatcherHelper.CheckBeginInvokeOnUI(() =>
                {
                    mediaItem.DurationDeciseconds = (int)md.Duration.TotalMilliseconds / 10;
                });
            }
        }

        private void PopulateThumbnail(MediaItem mediaItem)
        {
            if (mediaItem.ThumbnailImageSource == null)
            {
                // ReSharper disable once StyleCop.SA1117
                var thumb = _thumbnailService.GetThumbnail(
                    mediaItem.FilePath,
                    Unosquare.FFME.MediaElement.FFmpegDirectory,
                    mediaItem.MediaType.Classification,
                    mediaItem.LastChanged,
                    out var _);

                DispatcherHelper.CheckBeginInvokeOnUI(() =>
                {
                    mediaItem.ThumbnailImageSource = GraphicsUtils.ByteArrayToImage(thumb);
                });
            }
        }
    }
}
