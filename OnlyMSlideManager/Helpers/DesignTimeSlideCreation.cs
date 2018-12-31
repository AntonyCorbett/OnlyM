namespace OnlyMSlideManager.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Windows.Media.Imaging;
    using OnlyMSlideManager.Models;
    using OnlyMSlideManager.Properties;

    internal static class DesignTimeSlideCreation
    {
        public static IReadOnlyCollection<SlideItem> GenerateSlides(int count)
        {
            var result = new List<SlideItem>();

            var image = Resources.flower;

            for (int n = 0; n < count; ++n)
            {
                result.Add(new SlideItem
                {
                    Name = $"Slide {n + 1}",
                    Image = GraphicsUtils.Convert(image)
                });
            }

            return result;
        }
    }
}
