namespace OnlyM.Services.ThumbnailQueue
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

    internal class ThumbnailQueueConsumer
    {
        private readonly IThumbnailService _thumbnailService;
        private readonly BlockingCollection<MediaItem> _collection;
        private readonly CancellationToken _cancellationToken;

        public ThumbnailQueueConsumer(
            IThumbnailService thumbnailService,
            BlockingCollection<MediaItem> thumbnailProducerCollection,
            CancellationToken cancellationToken)
        {
            _thumbnailService = thumbnailService;
            _collection = thumbnailProducerCollection;
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

                            Log.Logger.Verbose("Thumbs queue size (consumer) = {QueueSize}", _collection.Count);
                        }
                    }
                    catch (OperationCanceledException ex)
                    {
                        Log.Logger.Error(ex, "thumbnail consumer cancelled");
                    }
                }, 
                _cancellationToken);
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
