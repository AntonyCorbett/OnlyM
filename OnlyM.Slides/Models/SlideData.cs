namespace OnlyM.Slides.Models
{
    using System.Windows.Media.Imaging;

    public class SlideData
    {
        public string ArchiveEntryName { get; set; }

        public bool FadeInForward { get; set; }

        public bool FadeInReverse { get; set; }

        public bool FadeOutForward { get; set; }

        public bool FadeOutReverse { get; set; }

        public int DwellTimeMilliseconds { get; set; }

        public BitmapImage Image { get; set; }
    }
}
