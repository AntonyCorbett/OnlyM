namespace OnlyMSlideManager.Helpers
{
    using System.Collections.Generic;
    using System.Drawing;
    using System.Windows.Media;
    using OnlyM.CoreSys;
    using OnlyMSlideManager.Models;
    using OnlyMSlideManager.Properties;

    internal static class DesignTimeSlideCreation
    {
        public static IReadOnlyCollection<SlideItem> GenerateSlides(
            int count, int thumbnailWidth, int thumbnailHeight)
        {
            var result = new List<SlideItem>();

            var image = Resources.flower;

            for (int n = 0; n < count; ++n)
            {
                result.Add(new SlideItem
                {
                    Name = $"Slide {n + 1}",
                    ThumbnailImage = CreateThumbnailImage(image, thumbnailWidth, thumbnailHeight),
                });
            }

            result.Add(new SlideItem { IsEndMarker = true });

            return result;
        }

        private static ImageSource CreateThumbnailImage(Bitmap image, int thumbnailWidth, int thumbnailHeight)
        {
            var bmp = GraphicsUtils.BitmapToBitmapImage(image);
            return GraphicsUtils.Downsize(bmp, thumbnailWidth, thumbnailHeight);
        }
    }
}
