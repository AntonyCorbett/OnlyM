using System;
using System.Windows.Media.Imaging;

namespace OnlyM.Services.ImagesCache
{
    internal class ImageAndLastUsed
    {
        public BitmapSource BitmapImage { get; set; }

        public DateTime LastUsedUtc { get; set; }
    }
}
