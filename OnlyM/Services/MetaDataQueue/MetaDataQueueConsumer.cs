namespace OnlyM.Services.MetaDataQueue
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;
    using Core.Models;
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
        private readonly BlockingCollection<MediaItem> _problemFiles;
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

            _problemFiles = new BlockingCollection<MediaItem>();
        }

        public void Execute()
        {
            RunMainCollectionConsumer();
            RunProblemFilesConsumer();
        }

        private void RunMainCollectionConsumer()
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
                            if (!PopulateThumbnailAndMetaData(nextItem))
                            {
                                // put it back in the problem queue!
                                Log.Logger.Debug($"Placed in problem queue {nextItem.FilePath}");
                                _problemFiles.Add(nextItem, _cancellationToken);
                            }
                            else
                            {
                                Log.Logger.Debug($"Done item {nextItem.FilePath}");
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

        private void RunProblemFilesConsumer()
        {
            Task.Run(
                () =>
                {
                    try
                    {
                        while (!_cancellationToken.IsCancellationRequested)
                        {
                            var nextItem = _problemFiles.Take(_cancellationToken);

                            if (!PopulateThumbnailAndMetaData(nextItem))
                            {
                                Thread.Sleep(2000);
                                _problemFiles.Add(nextItem, _cancellationToken);
                            }
                            else
                            {
                                Log.Logger.Debug($"Done item {nextItem.FilePath}");
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        Log.Logger.Debug("Metadata 'problem files' consumer closed");
                    }
                },
                _cancellationToken);
        }

        private bool PopulateThumbnailAndMetaData(MediaItem mediaItem)
        {
            byte[] thumb = null;

            var metaData = _metaDataService.GetMetaData(mediaItem.FilePath, mediaItem.MediaType.Classification);
            if (metaData == null)
            {
                return false;
            }

            if (mediaItem.ThumbnailImageSource == null)
            {
                // ReSharper disable once StyleCop.SA1117
                thumb = _thumbnailService.GetThumbnail(
                    mediaItem.FilePath,
                    Unosquare.FFME.MediaElement.FFmpegDirectory,
                    mediaItem.MediaType.Classification,
                    mediaItem.LastChanged,
                    out var _);

                if (thumb == null)
                {
                    return false;
                }
            }

            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                mediaItem.ThumbnailImageSource = GraphicsUtils.ByteArrayToImage(thumb);
                mediaItem.DurationDeciseconds = (int)metaData.Duration.TotalMilliseconds / 10;
            });

            return true;
        }
    }
}
