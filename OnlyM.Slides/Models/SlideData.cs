namespace OnlyM.Slides.Models
{
    using System.Windows.Media.Imaging;

    public class SlideData
    {
        public string ArchiveEntryName { get; set; }

        public bool FadeIn { get; set; }

        public bool FadeOut { get; set; }

        public int DwellTimeMilliseconds { get; set; }

        public BitmapImage Image { get; set; }
    }
}
