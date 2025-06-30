using System;
using System.Windows.Media.Imaging;

namespace OnlyM.Services.ImagesCache;

internal sealed class ImageAndLastUsed
{
    public BitmapSource? BitmapImage { get; init; }

    public DateTime LastUsedUtc { get; set; }
}
