using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using OnlyM.Core.Models;
using OnlyM.Core.Services.Media;
using OnlyM.Core.Services.Options;
using OnlyM.CoreSys;
using OnlyM.Models;
using OnlyM.Slides;
using Serilog;

namespace OnlyM.Services.MetaDataQueue;

internal sealed class MetaDataQueueConsumer : IDisposable
{
    private readonly IThumbnailService _thumbnailService;
    private readonly IMediaMetaDataService _metaDataService;
    private readonly IOptionsService _optionsService;

    private readonly BlockingCollection<MediaItem> _collection;
    private readonly CancellationToken _cancellationToken;
    private readonly string _ffmpegFolder;

    public MetaDataQueueConsumer(
        IThumbnailService thumbnailService,
        IMediaMetaDataService metaDataService,
        IOptionsService optionsService,
        BlockingCollection<MediaItem> metaDataProducerCollection,
        string ffmpegFolder,
        CancellationToken cancellationToken)
    {
        _thumbnailService = thumbnailService;
        _metaDataService = metaDataService;
        _optionsService = optionsService;

        _ffmpegFolder = ffmpegFolder;

        _collection = metaDataProducerCollection;
        _cancellationToken = cancellationToken;
    }

    public event EventHandler<ItemMetaDataPopulatedEventArgs>? ItemCompletedEvent;

    public event EventHandler? AllItemsCompletedEvent;

    public void Execute() => RunConsumer();

    public void Dispose() => _collection.Dispose();

    private void RunConsumer() => Task.Run(RunConsumerTaskAsync, _cancellationToken);

    private async Task RunConsumerTaskAsync()
    {
        try
        {
            while (!_cancellationToken.IsCancellationRequested)
            {
                var nextItem = _collection.Take(_cancellationToken);

                Log.Logger.Debug("Consuming item {Path}", nextItem.FilePath);

                if (!IsPopulated(nextItem))
                {
                    await PopulateThumbnailAndMetaDataAsync(nextItem);

                    if (!IsPopulated(nextItem))
                    {
                        // put it back in the queue!
                        ReplaceInQueue(nextItem);
                    }
                    else
                    {
                        ItemCompleted(nextItem);
                    }

                    Log.Logger.Verbose("Metadata queue size (consumer) = {QueueSize}", _collection.Count);

                    if (_collection.Count == 0)
                    {
                        AllItemsCompletedEvent?.Invoke(this, EventArgs.Empty);
                    }
                }
                else
                {
                    ItemCompleted(nextItem);
                }
            }
        }
        catch (OperationCanceledException)
        {
            Log.Logger.Debug("Metadata consumer closed");
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Running MetaDataQueueConsumer");
        }
    }

    private void ItemCompleted(MediaItem nextItem)
    {
        Log.Logger.Debug("Done item {Path}", nextItem.FilePath);
        ItemCompletedEvent?.Invoke(this, new ItemMetaDataPopulatedEventArgs { MediaItem = nextItem });
    }

    private void ReplaceInQueue(MediaItem mediaItem) =>
        Task.Delay(2000, _cancellationToken)
            .ContinueWith(
                _ =>
                {
                    Log.Logger.Debug("Replaced in queue {Path}", mediaItem.FilePath);
                    _collection.Add(mediaItem, _cancellationToken);
                },
                _cancellationToken);

    private Task PopulateThumbnailAndMetaDataAsync(MediaItem mediaItem) =>
        Application.Current.Dispatcher.Invoke(async () =>
        {
            await PopulateSlideDataAsync(mediaItem);
            await PopulateThumbnailAsync(mediaItem);
            await PopulateDurationAndTitleAsync(mediaItem);
        });

    private async Task PopulateSlideDataAsync(MediaItem mediaItem)
    {
        if (!IsSlideDataPopulated(mediaItem) && mediaItem.FilePath != null)
        {
            SlideFile? sf = null;

            await Task.Run(() => sf = new SlideFile(mediaItem.FilePath), _cancellationToken);

            if (sf != null && !_cancellationToken.IsCancellationRequested)
            {
                mediaItem.SlideshowCount = sf.SlideCount;
                mediaItem.SlideshowLoop = sf.Loop;
                mediaItem.IsRollingSlideshow = sf.AutoPlay;
            }
        }
    }

    private static bool IsPopulated(MediaItem mediaItem) =>
        IsThumbnailPopulated(mediaItem) &&
        IsDurationAndTitlePopulated(mediaItem) &&
        IsSlideDataPopulated(mediaItem);

    private static bool IsThumbnailPopulated(MediaItem mediaItem) => mediaItem.ThumbnailImageSource != null;

    private static bool IsDurationAndTitlePopulated(MediaItem mediaItem) =>
        (!mediaItem.HasDuration || mediaItem.DurationDeciseconds > 0) &&
        !string.IsNullOrEmpty(mediaItem.Title);

    private static bool IsSlideDataPopulated(MediaItem mediaItem) => !mediaItem.IsSlideshow || mediaItem.SlideshowCount > 0;

    private async Task PopulateDurationAndTitleAsync(MediaItem mediaItem)
    {
        if (mediaItem.FilePath != null &&
            mediaItem.MediaType != null &&
            !IsDurationAndTitlePopulated(mediaItem))
        {
            MediaMetaData? metaData = null;

            await Task.Run(() => metaData = _metaDataService.GetMetaData(mediaItem.FilePath, mediaItem.MediaType, _ffmpegFolder), _cancellationToken);

            if (!IsDurationAndTitlePopulated(mediaItem) && !_cancellationToken.IsCancellationRequested)
            {
                mediaItem.DurationDeciseconds = metaData == null ? 0 : (int)(metaData.Duration.TotalSeconds * 10);
                mediaItem.Title = GetMediaTitle(mediaItem.FilePath, metaData);
                mediaItem.FileNameAsSubTitle = _optionsService.UseInternalMediaTitles
                    ? Path.GetFileName(mediaItem.FilePath)
                    : null;
                mediaItem.VideoRotation = metaData?.VideoRotation ?? 0;
            }
        }
    }

    private async Task PopulateThumbnailAsync(MediaItem mediaItem)
    {
        if (mediaItem.FilePath != null && mediaItem.MediaType != null && !IsThumbnailPopulated(mediaItem))
        {
            byte[]? thumb = null;

            await Task.Run(
                () =>
                {
                    thumb = _thumbnailService.GetThumbnail(
                        mediaItem.FilePath,
                        Unosquare.FFME.Library.FFmpegDirectory,
                        mediaItem.MediaType.Classification,
                        mediaItem.LastChanged,
                        out var _);
                }, _cancellationToken);

            if (thumb != null && !IsThumbnailPopulated(mediaItem) && !_cancellationToken.IsCancellationRequested)
            {
                mediaItem.ThumbnailImageSource = GraphicsUtils.ByteArrayToImage(thumb);
            }
        }
    }

    private string GetMediaTitle(string filePath, MediaMetaData? metaData)
    {
        if (_optionsService.UseInternalMediaTitles && metaData != null && !string.IsNullOrEmpty(metaData.Title))
        {
            return metaData.Title;
        }

        return Path.GetFileNameWithoutExtension(filePath);
    }
}
