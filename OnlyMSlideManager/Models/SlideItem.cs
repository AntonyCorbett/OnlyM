using Microsoft.Toolkit.Mvvm.ComponentModel;

namespace OnlyMSlideManager.Models
{
    using System;
    using System.Windows.Media;

    public sealed class SlideItem : ObservableObject
    {
        private bool _showCardBorder;
        private bool _fadeInForward;
        private bool _fadeOutForward;
        private bool _fadeInReverse;
        private bool _fadeOutReverse;
        private int? _dwellTimeSeconds;

        public event EventHandler SlideItemModifiedEvent;
        
        public string Name { get; set; }

        public bool IsEndMarker { get; set; }

        public string OriginalFilePath { get; set; }

        public ImageSource ThumbnailImage { get; set; }

        public int SlideIndex { get; set; }

        public bool FadeInForward
        {
            get => _fadeInForward;
            set
            {
                if (_fadeInForward != value)
                {
                    _fadeInForward = value;
                    OnPropertyChanged();
                    OnSlideItemModifiedEvent();
                }
            }
        }

        public bool FadeInReverse
        {
            get => _fadeInReverse;
            set
            {
                if (_fadeInReverse != value)
                {
                    _fadeInReverse = value;
                    OnPropertyChanged();
                    OnSlideItemModifiedEvent();
                }
            }
        }

        public bool FadeOutForward
        {
            get => _fadeOutForward;
            set
            {
                if (_fadeOutForward != value)
                {
                    _fadeOutForward = value;
                    OnPropertyChanged();
                    OnSlideItemModifiedEvent();
                }
            }
        }

        public bool FadeOutReverse
        {
            get => _fadeOutReverse;
            set
            {
                if (_fadeOutReverse != value)
                {
                    _fadeOutReverse = value;
                    OnPropertyChanged();
                    OnSlideItemModifiedEvent();
                }
            }
        }

        public int? DwellTimeSeconds
        {
            get => _dwellTimeSeconds;
            set
            {
                if (_dwellTimeSeconds != value)
                {
                    _dwellTimeSeconds = value;
                    OnPropertyChanged();
                    OnSlideItemModifiedEvent();
                }
            }
        }

        public bool ShowCardBorder
        {
            get => _showCardBorder;
            set
            {
                if (_showCardBorder != value)
                {
                    _showCardBorder = value;
                    OnPropertyChanged();
                }
            }
        }

        public string DropZoneId { get; set; }

        private void OnSlideItemModifiedEvent()
        {
            SlideItemModifiedEvent?.Invoke(this, EventArgs.Empty);
        }
    }
}
