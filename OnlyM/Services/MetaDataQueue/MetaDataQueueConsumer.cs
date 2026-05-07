using System;
using System.IO;
using System.Threading;
using System.Threading.Channels;
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

internal sealed class MetaDataQueueConsumer
{
    private const int MaxMetaDataRetries = 3;

    private readonly IThumbnailService _thumbnailService;
    private readonly IMediaMetaDataService _metaDataService;
    private readonly IOptionsService _optionsService;

    private readonly MetaDataQueueProducer _producer;
    private readonly ChannelReader<MediaItem> _reader;
    private readonly CancellationToken _cancellationToken;
    private readonly string _ffmpegFolder;

    private readonly int _maxDegreeOfParallelism;
    private readonly Lock _allItemsEventLock = new();
    private int _inProgressCount;
    private bool _allItemsEventRaisedForCurrentBatch;

    public MetaDataQueueConsumer(
        IThumbnailService thumbnailService,
        IMediaMetaDataService metaDataService,
        IOptionsService optionsService,
        MetaDataQueueProducer producer,
        string ffmpegFolder,
        int maxDegreeOfParallelism,
        CancellationToken cancellationToken)
    {
        _thumbnailService = thumbnailService;
        _metaDataService = metaDataService;
        _optionsService = optionsService;

        _ffmpegFolder = ffmpegFolder;

        _producer = producer;
        _reader = producer.Reader;
        _cancellationToken = cancellationToken;

        _maxDegreeOfParallelism = Math.Max(1, maxDegreeOfParallelism);
    }

    public event EventHandler<ItemMetaDataPopulatedEventArgs>? ItemCompletedEvent;

    public event EventHandler? AllItemsCompletedEvent;

    public void Execute() => RunConsumers();

    private void RunConsumers()
    {
        for (var i = 0; i < _maxDegreeOfParallelism; i++)
        {
            Task.Run(RunConsumerTaskAsync, _cancellationToken);
        }
    }

    private async Task RunConsumerTaskAsync()
    {
        try
        {
            while (!_cancellationToken.IsCancellationRequested)
            {
                MediaItem nextItem;
                try
                {
                    nextItem = await _reader.ReadAsync(_cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                _producer.ItemDequeued(nextItem);
                ResetAllItemsCompletedFlag();

                Interlocked.Increment(ref _inProgressCount);
                try
                {
                    if (Log.IsEnabled(LogEventLevel.Debug))
                    {
                        Log.Logger.Debug("Consuming item {Path}", nextItem.FilePath);
                    }

                    if (!IsPopulated(nextItem))
                    {
                        await PopulateThumbnailAndMetaDataAsync(nextItem);

                        if (!IsPopulated(nextItem))
                        {
                            ReplaceInQueue(nextItem);
                        }
                        else
                        {
                            ItemCompleted(nextItem);
                        }

                        if (Log.Logger.IsEnabled(LogEventLevel.Verbose))
                        {
                            Log.Logger.Verbose("Metadata queue size (consumer) = {QueueSize}", _reader.Count);
                        }
                    }
                    else
                    {
                        ItemCompleted(nextItem);
                    }
                }
                catch (Exception ex)
                {
                    EventTracker.Error(ex, "Error processing metadata item");
                    Log.Logger.Error(ex, "Error processing metadata item {Path}", nextItem.FilePath);
                }
                finally
                {
                    Interlocked.Decrement(ref _inProgressCount);
                    TryRaiseAllItemsCompleted();
                }

                // yield occasionally to avoid monopolizing threads if cancellation requested late
                await Task.Yield();
            }
        }
        catch (OperationCanceledException)
        {
            Log.Logger.Debug("Metadata consumer cancelled");
        }
        catch (Exception ex)
        {
            EventTracker.Error(ex, "Running MetaDataQueueConsumer");
            Log.Logger.Error(ex, "Running MetaDataQueueConsumer");
        }
    }

    private void ItemCompleted(MediaItem nextItem)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Logger.Debug("Done item {Path}", nextItem.FilePath);
        }

        ItemCompletedEvent?.Invoke(this, new ItemMetaDataPopulatedEventArgs { MediaItem = nextItem });
    }

    private void ReplaceInQueue(MediaItem mediaItem)
    {
        // this is a retry mechanism for cases where metadata extraction fails (e.g. file locked)
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
                    if (_cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    Log.Logger.Debug("Replaced in queue {Path}", mediaItem.FilePath);
                    ResetAllItemsCompletedFlag();
                    _producer.Add(mediaItem);
                },
                _cancellationToken);
    }

    private void TryRaiseAllItemsCompleted()
    {
        if (_reader.Count == 0 && Volatile.Read(ref _inProgressCount) == 0)
        {
            lock (_allItemsEventLock)
            {
                if (_reader.Count == 0 && _inProgressCount == 0 && !_allItemsEventRaisedForCurrentBatch)
                {
                    _allItemsEventRaisedForCurrentBatch = true;
                    AllItemsCompletedEvent?.Invoke(this, EventArgs.Empty);
                }
            }
        }
    }

    private void ResetAllItemsCompletedFlag()
    {
        lock (_allItemsEventLock)
        {
            _allItemsEventRaisedForCurrentBatch = false;
        }
    }

    private async Task PopulateThumbnailAndMetaDataAsync(MediaItem mediaItem)
    {
        if (_cancellationToken.IsCancellationRequested)
        {
            return;
        }

        await PopulateSlideDataAsync(mediaItem);

        if (_cancellationToken.IsCancellationRequested)
        {
            return;
        }

        await PopulateThumbnailAsync(mediaItem);

        if (_cancellationToken.IsCancellationRequested)
        {
            return;
        }

        await PopulateDurationAndTitleAsync(mediaItem);
    }

    private async Task PopulateSlideDataAsync(MediaItem mediaItem)
    {
        if (_cancellationToken.IsCancellationRequested ||
            IsSlideDataPopulated(mediaItem) ||
            mediaItem.FilePath == null)
        {
            return;
        }

        try
        {
            var sf = new SlideFile(mediaItem.FilePath);
            if (_cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                mediaItem.SlideshowCount = sf.SlideCount;
                mediaItem.SlideshowLoop = sf.Loop;
                mediaItem.IsRollingSlideshow = sf.AutoPlay;
            });
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Could not parse slideshow file {Path}", mediaItem.FilePath);
        }
    }

    private static bool IsPopulated(MediaItem mediaItem)
    {
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
        if (_cancellationToken.IsCancellationRequested ||
            mediaItem.FilePath == null ||
            mediaItem.MediaType == null ||
            IsDurationAndTitlePopulated(mediaItem))
        {
            return;
        }

        MediaMetaData? metaData = null;

        try
        {
            metaData = _metaDataService.GetMetaData(
                mediaItem.FilePath,
                mediaItem.MediaType,
                _ffmpegFolder);
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Metadata extraction failed for {Path}", mediaItem.FilePath);
        }

        if (IsDurationAndTitlePopulated(mediaItem) || _cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var durationDeciseconds = metaData == null ? 0 : (int)(metaData.Duration.TotalSeconds * 10);
        var title = GetMediaTitle(mediaItem.FilePath, metaData);
        var fileNameAsSubTitle = _optionsService.UseInternalMediaTitles
            ? Path.GetFileName(mediaItem.FilePath)
            : null;
        var videoRotation = metaData?.VideoRotation ?? 0;

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if (!IsDurationAndTitlePopulated(mediaItem) && !_cancellationToken.IsCancellationRequested)
            {
                mediaItem.DurationDeciseconds = durationDeciseconds;
                mediaItem.Title = title;
                mediaItem.FileNameAsSubTitle = fileNameAsSubTitle;
                mediaItem.VideoRotation = videoRotation;
            }
        });
    }

    private async Task PopulateThumbnailAsync(MediaItem mediaItem)
    {
        if (_cancellationToken.IsCancellationRequested ||
            mediaItem.FilePath == null ||
            mediaItem.MediaType == null ||
            IsThumbnailPopulated(mediaItem))
        {
            return;
        }

        try
        {
            var thumb = _thumbnailService.GetThumbnail(
                mediaItem.FilePath,
                Unosquare.FFME.Library.FFmpegDirectory,
                mediaItem.MediaType.Classification,
                mediaItem.LastChanged,
                out var _);

            if (thumb == null || _cancellationToken.IsCancellationRequested || IsThumbnailPopulated(mediaItem))
            {
                return;
            }

            var bmp = GraphicsUtils.ByteArrayToImage(thumb);
            if (bmp == null)
            {
                return;
            }

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (!IsThumbnailPopulated(mediaItem))
                {
                    mediaItem.ThumbnailImageSource = bmp;
                }
            });
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Could not get a thumbnail for {Path}", mediaItem.FilePath);
        }
    }

    private string GetMediaTitle(string filePath, MediaMetaData? metaData)
    {
        if (_optionsService.UseInternalMediaTitles &&
            metaData != null &&
            !string.IsNullOrEmpty(metaData.Title))
        {
            return metaData.Title;
        }

        return Path.GetFileNameWithoutExtension(filePath);
    }
}
