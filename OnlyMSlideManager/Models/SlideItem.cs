namespace OnlyMSlideManager.Models
{
    using System.Windows.Media;

    using GalaSoft.MvvmLight;

    public class SlideItem : ViewModelBase
    {
        public string Name { get; set; }

        public string OriginalFilePath { get; set; }

        public ImageSource Image { get; set; }
    }
}
