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
using OnlyM.EventTracking;
using OnlyM.Models;
using OnlyM.Slides;
using Serilog;
using Serilog.Events;

namespace OnlyM.Services.MetaDataQueue;

internal sealed class MetaDataQueueConsumer : IDisposable
{
    private const int MaxMetaDataRetries = 3;

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

                    if (Log.Logger.IsEnabled(LogEventLevel.Verbose))
                    {
                        Log.Logger.Verbose("Metadata queue size (consumer) = {QueueSize}", _collection.Count);
                    }

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
            EventTracker.Error(ex, "Running MetaDataQueueConsumer");
            Log.Logger.Error(ex, "Running MetaDataQueueConsumer");
        }
    }

    private void ItemCompleted(MediaItem nextItem)
    {
        Log.Logger.Debug("Done item {Path}", nextItem.FilePath);
        ItemCompletedEvent?.Invoke(this, new ItemMetaDataPopulatedEventArgs { MediaItem = nextItem });
    }

    private void ReplaceInQueue(MediaItem mediaItem)
    {
        mediaItem.MetaDataRetryCount++;
        if (mediaItem.MetaDataRetryCount > MaxMetaDataRetries)
        {
            Log.Logger.Warning("Max metadata retries exceeded for {Path}. Dropping from queue.", mediaItem.FilePath);
            return;
        }

        Task.Delay(2000, _cancellationToken)
            .ContinueWith(
                _ =>
                {
                    Log.Logger.Debug("Replaced in queue {Path}", mediaItem.FilePath);
                    _collection.Add(mediaItem, _cancellationToken);
                },
                _cancellationToken);
    }

    private async Task PopulateThumbnailAndMetaDataAsync(MediaItem mediaItem)
    {
        // Run logical steps sequentially. Each method internally does its own Task.Run
        // for CPU/IO work. We only dispatch the minimal UI mutations.
        await PopulateSlideDataAsync(mediaItem);
        await PopulateThumbnailAsync(mediaItem);
        await PopulateDurationAndTitleAsync(mediaItem);
    }

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

    private static bool IsPopulated(MediaItem mediaItem)
    {
        if (Log.Logger.IsEnabled(LogEventLevel.Debug))
        {
            Log.Logger.Debug(
                "Thumb: {IsThumbnailPopulated} Duration and Title: {IsDurationAndTitlePopulated} Slide: {IsSlideDataPopulated}",
                IsThumbnailPopulated(mediaItem),
                IsDurationAndTitlePopulated(mediaItem),
                IsSlideDataPopulated(mediaItem));
        }

        return
            IsThumbnailPopulated(mediaItem) &&
            IsDurationAndTitlePopulated(mediaItem) &&
            IsSlideDataPopulated(mediaItem);
    }

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
            try
            {
                // Generate / fetch thumbnail bytes off the UI thread.
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

                Log.Logger.Debug(
                    "PopulateThumbnailAsync: thumb={Thumb} IsThumbnailPopulated={IsPopulated} Cancelled={Cancelled}",
                    thumb != null,
                    IsThumbnailPopulated(mediaItem),
                    _cancellationToken.IsCancellationRequested);

                if (thumb != null && !_cancellationToken.IsCancellationRequested && !IsThumbnailPopulated(mediaItem))
                {
                    // Create (and freeze) the BitmapImage off UI thread
                    var bmp = GraphicsUtils.ByteArrayToImage(thumb);

                    if (bmp != null)
                    {
                        // Assign on UI thread (safer for bindings / PropertyChanged sequencing)
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            if (!IsThumbnailPopulated(mediaItem))
                            {
                                mediaItem.ThumbnailImageSource = bmp;
                            }
                        });
                        Log.Logger.Debug("After assignment: ThumbnailImageSource now set = {Value}", mediaItem.ThumbnailImageSource != null);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Could not get a thumbnail for {Path}", mediaItem.FilePath);
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
