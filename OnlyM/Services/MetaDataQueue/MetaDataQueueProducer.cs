using System;
using System.Collections.Concurrent;
using System.Linq;
using OnlyM.Models;
using Serilog;

namespace OnlyM.Services.MetaDataQueue;

internal sealed class MetaDataQueueProducer : IDisposable
{
    public BlockingCollection<MediaItem> Queue { get; } = new();

    public void Add(MediaItem mediaItem)
    {
        // limit any duplication...
        if (!Queue.Contains(mediaItem))
        {
            Queue.TryAdd(mediaItem);

            Log.Logger.Verbose("Metadata queue size = {QueueSize}", Queue.Count);
        }
    }

    public void Dispose() => Queue?.Dispose();
}