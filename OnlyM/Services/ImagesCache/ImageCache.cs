using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using OnlyM.CoreSys;
using Serilog;

namespace OnlyM.Services.ImagesCache;

internal sealed class ImageCache
{
    private const int MaxItemCount = 18;
    private const int PurgeCount = 6;
    private const int MaxImageWidth = 1920;
    private const int MaxImageHeight = 1080;

    private readonly ConcurrentDictionary<string, ImageAndLastUsed> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    public BitmapSource? GetImage(string fullPath)
    {
        var info = new FileInfo(fullPath);
        if (!info.Exists)
        {
            return null;
        }

        return InternalGetImage(fullPath, info.LastWriteTimeUtc.Ticks);
    }

    private BitmapSource? InternalGetImage(string fullPath, long lastChangedDate)
    {
        var cacheKey = GetCacheKey(fullPath, lastChangedDate);

        if (!_cache.TryGetValue(cacheKey, out var result))
        {
            try
            {
                var image = GraphicsUtils.Downsize(fullPath, MaxImageWidth, MaxImageHeight, ignoreInternalCache: true);
                result = new ImageAndLastUsed { BitmapImage = image, LastUsedUtc = DateTime.UtcNow };

                _cache.AddOrUpdate(
                    cacheKey,
                    result,
                    (_, value) =>
                    {
                        value.LastUsedUtc = DateTime.UtcNow;
                        return value;
                    });

                if (_cache.Count > MaxItemCount)
                {
                    RemoveOldImages();
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, $"Could not cache image {fullPath}");
            }
        }
        else
        {
            Log.Logger.Debug($"Image in cache: {fullPath}");
            result.LastUsedUtc = DateTime.UtcNow;
        }

        return result?.BitmapImage;
    }

    private static string GetCacheKey(string fullPath, long lastChangedDate) => $"{fullPath}|{lastChangedDate}";

    private void RemoveOldImages()
    {
        var oldItems = _cache.Select(x => x).OrderBy(pair => pair.Value.LastUsedUtc).Take(PurgeCount);
        foreach (var oldItem in oldItems)
        {
            _cache.TryRemove(oldItem.Key, out _);
        }
    }
}