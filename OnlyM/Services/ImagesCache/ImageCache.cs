using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using OnlyM.CoreSys;
using Serilog;

namespace OnlyM.Services.ImagesCache
{
    internal class ImageCache
    {
        private const int MaxItemCount = 18;
        private const int PurgeCount = 6;
        private const int MaxImageWidth = 1920;
        private const int MaxImageHeight = 1080;

        private readonly ConcurrentDictionary<string, ImageAndLastUsed> _cache = 
            new(StringComparer.OrdinalIgnoreCase);

        public BitmapSource GetImage(string fullPath)
        {
            if (!_cache.TryGetValue(fullPath, out var result))
            {
                if (!File.Exists(fullPath))
                {
                    return null;
                }

                try
                {
                    var image = GraphicsUtils.Downsize(fullPath, MaxImageWidth, MaxImageHeight);
                    if (image != null)
                    {
                        result = new ImageAndLastUsed { BitmapImage = image, LastUsedUtc = DateTime.UtcNow };

                        _cache.AddOrUpdate(
                            fullPath,
                            result,
                            (s, value) =>
                            {
                                value.LastUsedUtc = DateTime.UtcNow;
                                return value;
                            });
                    }

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

        private void RemoveOldImages()
        {
            var oldItems = _cache.Select(x => x).OrderBy(pair => pair.Value.LastUsedUtc).Take(PurgeCount);
            foreach (var oldItem in oldItems)
            {
                _cache.TryRemove(oldItem.Key, out _);
            }
        }
    }
}
