using System.Collections.Generic;
using System.Drawing;
using System.Windows.Media;
using OnlyM.CoreSys;
using OnlyMSlideManager.Models;
using OnlyMSlideManager.Properties;

namespace OnlyMSlideManager.Helpers
{
    internal static class DesignTimeSlideCreation
    {
        public static IReadOnlyCollection<SlideItem> GenerateSlides(
            int count, int thumbnailWidth, int thumbnailHeight)
        {
            var result = new List<SlideItem>(count + 1);

            var image = Resources.flower;

            for (var n = 0; n < count; ++n)
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

        private static ImageSource? CreateThumbnailImage(Bitmap image, int thumbnailWidth, int thumbnailHeight)
        {
            var bmp = GraphicsUtils.BitmapToBitmapImage(image);
            return GraphicsUtils.Downsize(bmp, thumbnailWidth, thumbnailHeight);
        }
    }
}
