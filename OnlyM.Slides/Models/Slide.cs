namespace OnlyM.Slides.Models
{
    internal class Slide
    {
        public string ArchiveEntryName { get; set; }

        public string OriginalFilePath { get; set; }

        public bool FadeInForward { get; set; }

        public bool FadeInReverse { get; set; }

        public bool FadeOutForward { get; set; }

        public bool FadeOutReverse { get; set; }

        public int DwellTimeMilliseconds { get; set; }
    }
}
