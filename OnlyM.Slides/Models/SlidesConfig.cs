using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OnlyM.Slides.Models
{
    internal sealed class SlidesConfig
    {
        public List<Slide> Slides { get; } = new();

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
            const int oneSecond = 1000;
            const int tenSeconds = 10000;

            if (AutoPlay && DwellTimeMilliseconds < oneSecond)
            {
                DwellTimeMilliseconds = tenSeconds;
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

        public void SyncSlideOrder(IEnumerable<string?> slideNames)
        {
            var originalList = new List<Slide>(Slides);
            Slides.Clear();

            foreach (var slide in slideNames)
            {
                var originalSlide = originalList.SingleOrDefault(
                    x => x.ArchiveEntryName != null && 
                         x.ArchiveEntryName.Equals(slide, StringComparison.OrdinalIgnoreCase));

                if (originalSlide != null)
                {
                    Slides.Add(originalSlide);
                }
            }
        }

        public void RemoveSlide(string? slideName)
        {
            var slide = GetSlideByName(slideName);
            if (slide != null)
            {
                Slides.Remove(slide);
            }
        }

        private Slide? GetSlideByName(string? slideName)
        {
            return Slides.SingleOrDefault(
                x => x.ArchiveEntryName != null && 
                     x.ArchiveEntryName.Equals(slideName, StringComparison.Ordinal));
        }
    }
}
