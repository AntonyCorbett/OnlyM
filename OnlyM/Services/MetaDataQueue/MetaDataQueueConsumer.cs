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

    internal sealed class MetaDataQueueConsumer : IDisposable
    {
        private readonly IThumbnailService _thumbnailService;
        private readonly BlockingCollection<MediaItem> _collection;
        private readonly BlockingCollection<MediaItem> _problemFiles;
        private readonly CancellationToken _cancellationToken;

        public MetaDataQueueConsumer(
            IThumbnailService thumbnailService,
            BlockingCollection<MediaItem> metaDataProducerCollection,
            CancellationToken cancellationToken)
        {
            _thumbnailService = thumbnailService;
            
            _collection = metaDataProducerCollection;
            _cancellationToken = cancellationToken;

            _problemFiles = new BlockingCollection<MediaItem>();
        }

        public void Execute()
        {
            RunMainCollectionConsumer();
            RunProblemFilesConsumer();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Microsoft.Usage", 
            "CA2213:DisposableFieldsShouldBeDisposed", 
            MessageId = "_problemFiles",
            Justification = "False Positive")]
        public void Dispose()
        {
            _collection?.Dispose();
            _problemFiles?.Dispose();
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
                                // put it in the problem queue!
                                Log.Logger.Debug($"Placed in problem queue {nextItem.FilePath}");
                                _problemFiles.Add(nextItem, _cancellationToken);
                            }
                            else
                            {
                                Log.Logger.Debug($"Done item {nextItem.FilePath}");
                            }

                            Log.Logger.Verbose("Metadata queue size (consumer) = {QueueSize}", _collection.Count);

                            NotifyIfEmptyMetaDataQueue();
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

                            NotifyIfEmptyMetaDataQueue();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        Log.Logger.Debug("Metadata 'problem files' consumer closed");
                    }
                },
                _cancellationToken);
        }

        private void NotifyIfEmptyMetaDataQueue()
        {
            if (_collection.Count == 0 && _problemFiles.Count == 0)
            {
                // raise event if needed
            }
        }

        private bool PopulateThumbnailAndMetaData(MediaItem mediaItem)
        {
            byte[] thumb = null;

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
            });

            return true;
        }
    }
}
