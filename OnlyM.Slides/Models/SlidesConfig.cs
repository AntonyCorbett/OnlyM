namespace OnlyM.Slides.Models
{
    using System.Collections.Generic;

    internal class SlidesConfig
    {
        public List<Slide> Slides { get; } = new List<Slide>();

        public bool AutoPlay { get; set; }

        public int DwellTimeMilliseconds { get; set; }

        public bool Loop { get; set; }

        public int SlideCount => Slides.Count;

        public void Clear()
        {
            Slides.Clear();
            AutoPlay = false;
            DwellTimeMilliseconds = 0;
            Loop = false;
        }
    }
}
