namespace OnlyM.Services.MetaDataQueue
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using Models;
    using Serilog;

    internal sealed class MetaDataQueueProducer : IDisposable
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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "<Queue>k__BackingField", Justification = "False Positive")]
        public void Dispose()
        {
            Queue?.Dispose();
        }
    }
}
