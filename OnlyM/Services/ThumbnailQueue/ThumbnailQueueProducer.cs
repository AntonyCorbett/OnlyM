namespace OnlyM.Services.ThumbnailQueue
{
    using System.Collections.Concurrent;
    using System.Linq;
    using Models;
    using Serilog;

    internal class ThumbnailQueueProducer
    {
        private readonly BlockingCollection<MediaItem> _thumbnailQueue = new BlockingCollection<MediaItem>();

        public BlockingCollection<MediaItem> Collection => _thumbnailQueue;

        public void Add(MediaItem mediaItem)
        {
            if (!_thumbnailQueue.Contains(mediaItem))
            {
                // limit any duplication.
                _thumbnailQueue.Add(mediaItem);

                Log.Logger.Verbose("Thumbs queue size = {QueueSize}", _thumbnailQueue.Count);
            }
        }
    }
}
