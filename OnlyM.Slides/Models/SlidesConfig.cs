namespace OnlyM.Slides.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    internal class SlidesConfig
    {
        public List<Slide> Slides { get; } = new List<Slide>();

        public bool AutoPlay { get; set; }

        public int DwellTimeMilliseconds { get; set; }

        public bool Loop { get; set; }

        public bool AutoClose { get; set; }

        public int SlideCount => Slides.Count;

        public void Clear()
        {
            Slides.Clear();
            AutoPlay = false;
            DwellTimeMilliseconds = 0;
            Loop = false;
            AutoClose = false;
        }

        public void Sanitize()
        {
            const int OneSecond = 1000;
            const int TenSeconds = 10000;

            if (AutoPlay && DwellTimeMilliseconds < OneSecond)
            {
                DwellTimeMilliseconds = TenSeconds;
            }
        }

        public string CreateSignature()
        {
            var sb = new StringBuilder();

            sb.Append(AutoPlay);
            sb.Append('|');
            sb.Append(AutoClose);
            sb.Append('|');
            sb.Append(DwellTimeMilliseconds);
            sb.Append('|');
            sb.Append(Loop);
            sb.Append('|');

            foreach (var slide in Slides)
            {
                sb.Append(slide.CreateSignature());
            }

            return sb.ToString();
        }

        public void SyncSlideOrder(IEnumerable<string> slideNames)
        {
            var originalList = new List<Slide>(Slides);
            Slides.Clear();

            foreach (var slide in slideNames)
            {
                var originalSlide = originalList.SingleOrDefault(x => x.ArchiveEntryName.Equals(slide, StringComparison.OrdinalIgnoreCase));
                if (originalSlide != null)
                {
                    Slides.Add(originalSlide);
                }
            }
        }

        public void RemoveSlide(string slideName)
        {
            var slide = GetSlideByName(slideName);
            if (slide != null)
            {
                Slides.Remove(slide);
            }
        }

        private Slide GetSlideByName(string slideName)
        {
            return Slides.SingleOrDefault(x => x.ArchiveEntryName.Equals(slideName));
        }
    }
}
