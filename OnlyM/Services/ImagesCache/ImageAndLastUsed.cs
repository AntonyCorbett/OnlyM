namespace OnlyM.Services.ImagesCache
{
    using System;
    using System.Windows.Media.Imaging;

    internal class ImageAndLastUsed
    {
        public BitmapSource BitmapImage { get; set; }

        public DateTime LastUsedUtc { get; set; }
    }
}
