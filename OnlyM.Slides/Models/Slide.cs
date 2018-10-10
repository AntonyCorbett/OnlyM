namespace OnlyM.Slides.Models
{
    internal class Slide
    {
        public string ArchiveEntryName { get; set; }

        public string OriginalFilePath { get; set; }

        public bool FadeIn { get; set; }

        public bool FadeOut { get; set; }

        public int DwellTimeMilliseconds { get; set; }
    }
}
