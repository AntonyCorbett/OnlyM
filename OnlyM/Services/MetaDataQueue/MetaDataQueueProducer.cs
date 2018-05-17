namespace OnlyM.Services.MetaDataQueue
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Core.Models;
    using Models;
    using Serilog;

    internal class MetaDataQueueProducer
    {
        public BlockingCollection<MediaItem> Queue { get; } = new BlockingCollection<MediaItem>();

        public void Add(MediaItem mediaItem)
        {
            Task.Run(() =>
            {
                if (!Queue.Contains(mediaItem))
                {
                    if (!GetIdleFile(mediaItem))
                    {
                        Log.Logger.Error($"Timed out waiting for file: {mediaItem.FilePath}");
                        return;
                    }

                    // limit any duplication.
                    Queue.Add(mediaItem);

                    Log.Logger.Verbose("Metadata queue size = {QueueSize}", Queue.Count);
                }
            });
        }

        private TimeSpan GetIdleTimeout(MediaClassification mediaType)
        {
            switch (mediaType)
            {
                case MediaClassification.Image:
                    return TimeSpan.FromSeconds(10);

                case MediaClassification.Video:
                    return TimeSpan.FromMinutes(3);

                default:
                // ReSharper disable once RedundantCaseLabel
                case MediaClassification.Audio:
                    return TimeSpan.Zero;
            }
        }

        private bool GetIdleFile(MediaItem mediaItem)
        {
            var timeout = GetIdleTimeout(mediaItem.MediaType.Classification);
            if (timeout == TimeSpan.Zero)
            {
                return true;
            }
            
            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.Elapsed < timeout)
            {
                try
                {
                    using (File.Open(mediaItem.FilePath, FileMode.Open, FileAccess.ReadWrite))
                    {
                        return true;
                    }
                }
                catch
                {
                    Thread.Sleep(200);
                }
            }

            return false;
        }
    }
}
