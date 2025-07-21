using System;
using System.Collections.Concurrent;
using System.Linq;
using OnlyM.Models;
using Serilog;
using Serilog.Events;

namespace OnlyM.Services.MetaDataQueue;

internal sealed class MetaDataQueueProducer : IDisposable
{
    public BlockingCollection<MediaItem> Queue { get; } = [];

    public void Add(MediaItem mediaItem)
    {
        // limit any duplication...
        if (!Queue.Contains(mediaItem))
        {
            Queue.TryAdd(mediaItem);

            if (Log.Logger.IsEnabled(LogEventLevel.Verbose))
            {
                Log.Logger.Verbose("Metadata queue size = {QueueSize}", Queue.Count);
            }
        }
    }

    public void Dispose() => Queue?.Dispose();
}
