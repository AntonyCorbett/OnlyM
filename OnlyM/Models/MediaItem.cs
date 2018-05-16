namespace OnlyM.Models
{
    using System;
    using System.Windows.Media;
    using Core.Models;
    using GalaSoft.MvvmLight;

    public class MediaItem : ObservableObject
    {
        private static readonly SolidColorBrush ImageIconBrush = new SolidColorBrush(Colors.DarkGreen);
        private static readonly SolidColorBrush AudioIconBrush = new SolidColorBrush(Colors.CornflowerBlue);
        private static readonly SolidColorBrush VideoIconBrush = new SolidColorBrush(Colors.Chocolate);
        private static readonly SolidColorBrush UnknownIconBrush = new SolidColorBrush(Colors.Crimson);
        private static readonly SolidColorBrush GreenBrush = new SolidColorBrush(Colors.DarkGreen);
        private static readonly SolidColorBrush BlackBrush = new SolidColorBrush(Colors.Black);
        private static readonly SolidColorBrush GrayBrush = new SolidColorBrush(Colors.DarkGray);


        public Guid Id { get; set; }

        public string Name { get; set; }

        public string FilePath { get; set; }

        public long LastChanged { get; set; }

        private bool _isPaused;

        public bool IsPaused
        {
            get => _isPaused;

            set
            {
                if (_isPaused != value)
                {
                    _isPaused = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(PauseIconKind));
                    RaisePropertyChanged(nameof(HasDurationAndIsPlaying));
                    RaisePropertyChanged(nameof(HasDurationAndIsNotPlaying));
                }
            }
        }

        public string PauseIconKind =>
            IsPaused
                ? "Play"
                : "Pause";
        
        public SupportedMediaType MediaType { get; set; }

        private ImageSource _thumbnailImageSource;

        public ImageSource ThumbnailImageSource
        {
            get => _thumbnailImageSource;
            set
            {
                if (_thumbnailImageSource == null || !_thumbnailImageSource.Equals(value))
                {
                    _thumbnailImageSource = value;
                    RaisePropertyChanged();
                }
            }
        }

        private bool _isMediaActive;

        public bool IsMediaActive
        {
            get => _isMediaActive;
            set
            {
                if (_isMediaActive != value)
                {
                    _isMediaActive = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(HasDurationAndIsPlaying));
                    RaisePropertyChanged(nameof(HasDurationAndIsPlayingOrPaused));
                    RaisePropertyChanged(nameof(HasDurationAndIsNotPlaying));
                    RaisePropertyChanged(nameof(PlaybackTimeColorBrush));
                    RaisePropertyChanged(nameof(DurationColorBrush));
                }
            }
        }

        private bool _isMediaChanging;

        public bool IsMediaChanging
        {
            get => _isMediaChanging;
            set
            {
                if (_isMediaChanging != value)
                {
                    _isMediaChanging = value;
                    RaisePropertyChanged();
                }
            }
        }

        private bool _isPlayButtonEnabled;

        public bool IsPlayButtonEnabled
        {
            get => _isPlayButtonEnabled;
            set
            {
                if (_isPlayButtonEnabled != value)
                {
                    _isPlayButtonEnabled = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool HasDuration =>
            MediaType.Classification == MediaClassification.Audio ||
            MediaType.Classification == MediaClassification.Video;

        public bool HasDurationAndIsPlaying => HasDuration && IsMediaActive && !IsPaused;

        public bool HasDurationAndIsPlayingOrPaused => HasDuration && IsMediaActive;

        public bool HasDurationAndIsNotPlaying => HasDuration && (!IsMediaActive || IsPaused);


        private int _playbackPositionDeciseconds;

        public int PlaybackPositionDeciseconds
        {
            get => _playbackPositionDeciseconds;
            set
            {
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (_playbackPositionDeciseconds != value)
                {
                    _playbackPositionDeciseconds = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(PlaybackTimeString));
                }
            }
        }

        public string PlaybackTimeString
        {
            get
            {
                var ts = TimeSpan.FromMilliseconds(PlaybackPositionDeciseconds * 10);
                return ts.ToString(@"hh\:mm\:ss\.ff");
            }
        }

        public string DurationString
        {
            get
            {
                var ts = TimeSpan.FromMilliseconds(DurationDeciseconds * 10);
                return ts.ToString(@"hh\:mm\:ss\.ff");
            }
        }

        private int _durationDeciseconds;

        public int DurationDeciseconds
        {
            get => _durationDeciseconds;
            set
            {
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (_durationDeciseconds != value)
                {
                    _durationDeciseconds = value;
                    RaisePropertyChanged();
                }
            }
        }

        public Brush PlaybackTimeColorBrush =>
            IsMediaActive
                ? GreenBrush
                : GrayBrush;

        public Brush DurationColorBrush =>
            IsMediaActive
                ? BlackBrush
                : GrayBrush;

        public Brush IconBrush
        {
            get
            {
                switch (MediaType.Classification)
                {
                    case MediaClassification.Image:
                        return ImageIconBrush;

                    case MediaClassification.Video:
                        return VideoIconBrush;

                    case MediaClassification.Audio:
                        return AudioIconBrush;

                    default:
                        return UnknownIconBrush;
                }
            }
        }

        public string IconName
        {
            get
            {
                switch (MediaType.Classification)
                {
                    case MediaClassification.Image:
                        return "ImageFilterHdr";
                        
                    case MediaClassification.Video:
                        return "Video";

                    case MediaClassification.Audio:
                        return "VolumeHigh";

                    default:
                        return "Question";
                }
            }
        }
    }
}
