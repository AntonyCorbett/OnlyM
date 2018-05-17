namespace OnlyM.Services.MetaDataQueue
{
    using System.Collections.Concurrent;
    using System.Linq;
    using Models;
    using Serilog;

    internal class MetaDataQueueProducer
    {
        public BlockingCollection<MediaItem> Queue { get; } = new BlockingCollection<MediaItem>();

        public void Add(MediaItem mediaItem)
        {
            // limit any duplication...
            if (!Queue.Contains(mediaItem))
            {
                Queue.TryAdd(mediaItem);

                Log.Logger.Verbose("Metadata queue size = {QueueSize}", Queue.Count);
            }
        }
    }
}
