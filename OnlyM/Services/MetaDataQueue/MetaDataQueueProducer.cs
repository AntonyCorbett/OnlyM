using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using OnlyM.Models;
using Serilog;
using Serilog.Events;

namespace OnlyM.Services.MetaDataQueue;

internal sealed class MetaDataQueueProducer
{
    private readonly Lock _lock = new();
    private readonly HashSet<MediaItem> _pending = [];
    private readonly Channel<MediaItem> _channel = Channel.CreateUnbounded<MediaItem>(
        new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });

    public ChannelReader<MediaItem> Reader => _channel.Reader;

    public void Add(MediaItem mediaItem)
    {
        lock (_lock)
        {
            if (_pending.Add(mediaItem))
            {
                _channel.Writer.TryWrite(mediaItem);

                if (Log.Logger.IsEnabled(LogEventLevel.Verbose))
                {
                    Log.Logger.Verbose("Metadata queue size = {QueueSize}", _pending.Count);
                }
            }
        }
    }

    public void ItemDequeued(MediaItem mediaItem)
    {
        lock (_lock)
        {
            _pending.Remove(mediaItem);
        }
    }
}
