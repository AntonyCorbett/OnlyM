namespace OnlyMSlideManager.Models
{
    using System.Windows.Media;

    using GalaSoft.MvvmLight;

    public class SlideItem : ViewModelBase
    {
        private bool _showCardBorder;

        public string Name { get; set; }

        public bool IsEndMarker { get; set; }

        public string OriginalFilePath { get; set; }

        public ImageSource Image { get; set; }

        public bool FadeInForward { get; set; }

        public bool FadeInReverse { get; set; }

        public bool FadeOutForward { get; set; }

        public bool FadeOutReverse { get; set; }

        public int DwellTimeMilliseconds { get; set; }

        public bool ShowCardBorder
        {
            get => _showCardBorder;
            set
            {
                if (_showCardBorder != value)
                {
                    _showCardBorder = value;
                    RaisePropertyChanged();
                }
            }
        }

        public string DropZoneId { get; set; }
    }
}
