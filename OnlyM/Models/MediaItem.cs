namespace OnlyM.Models
{
    using System;
    using System.IO;
    using System.Windows.Media;
    using Core.Extensions;
    using Core.Models;
    using GalaSoft.MvvmLight;

    public sealed class MediaItem : ObservableObject
    {
        private static readonly SolidColorBrush ImageIconBrush = new SolidColorBrush(Colors.DarkGreen);
        private static readonly SolidColorBrush AudioIconBrush = new SolidColorBrush(Colors.CornflowerBlue);
        private static readonly SolidColorBrush VideoIconBrush = new SolidColorBrush(Colors.Chocolate);
        private static readonly SolidColorBrush SlideshowIconBrush = new SolidColorBrush(Colors.BlueViolet);
        private static readonly SolidColorBrush WebIconBrush = new SolidColorBrush(Colors.Firebrick);
        private static readonly SolidColorBrush UnknownIconBrush = new SolidColorBrush(Colors.Crimson);
        private static readonly SolidColorBrush GreenBrush = new SolidColorBrush(Colors.DarkGreen);
        private static readonly SolidColorBrush BlackBrush = new SolidColorBrush(Colors.Black);
        private static readonly SolidColorBrush GrayBrush = new SolidColorBrush(Colors.DarkGray);

        private bool _isMediaChanging;
        private bool _commandPanelVisible;
        private bool _pauseOnLastFrame;
        private bool _isCommandPanelOpen;
        private bool _allowFreezeCommand;
        private bool _useMirror;
        private bool _isVisible;
        private string _title;
        private bool _isPaused;
        private ImageSource _thumbnailImageSource;
        private bool _isMediaActive;
        private bool _isPlayButtonEnabled;
        private bool _allowPositionSeeking;
        private int _playbackPositionDeciseconds;
        private bool _allowPause;
        private string _playbackTimeString = GenerateTimeString(0);
        private int _durationDeciseconds;
        private int _currentSlideshowIndex;
        private int _slideshowCount;
        private bool _isRollingSlideshow;
        private bool _allowUseMirror;
        private string _miscText;
        private string _fileNameAsSubTitle;

        public event EventHandler PlaybackPositionChangedEvent;

        public Guid Id { get; set; }

        public bool IsVideo => MediaType.Classification == MediaClassification.Video;

        public bool IsWeb => MediaType.Classification == MediaClassification.Web;

        public bool IsWebAndAllowMirror => IsWeb && AllowUseMirror;
        
        public bool IsPdf => FilePath != null && Path.GetExtension(FilePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase);

        public bool IsBlankScreen { get; set; }

        public bool UseMirror
        {
            get => _useMirror;
            set
            {
                if (_useMirror != value)
                {
                    _useMirror = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool AllowUseMirror
        {
            get => _allowUseMirror;
            set
            {
                if (_allowUseMirror != value)
                {
                    _allowUseMirror = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(IsWebAndAllowMirror));
                }
            }
        }

        public bool CommandPanelVisible
        {
            get => _commandPanelVisible;
            set
            {
                if (_commandPanelVisible != value)
                {
                    _commandPanelVisible = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(CommandPanelBtnColWidth));

                    if (!_commandPanelVisible && IsCommandPanelOpen)
                    {
                        IsCommandPanelOpen = false;
                    }
                }
            }
        }
        
        public bool PauseOnLastFrame
        {
            get => _pauseOnLastFrame;
            set
            {
                if (_pauseOnLastFrame != value)
                {
                    _pauseOnLastFrame = value;
                    RaisePropertyChanged();
                }
            }
        }
        
        public bool AllowFreezeCommand
        {
            get => _allowFreezeCommand;
            set
            {
                if (_allowFreezeCommand != value)
                {
                    _allowFreezeCommand = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(ShouldDisplayFreezeCommand));
                }
            }
        }

        public bool ShouldDisplayFreezeCommand => IsVideo && AllowFreezeCommand;

        public bool IsCommandPanelOpen
        {
            get => _isCommandPanelOpen;
            set
            {
                if (_isCommandPanelOpen != value)
                {
                    _isCommandPanelOpen = value;
                    RaisePropertyChanged();
                }
            }
        }
        
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (_isVisible != value)
                {
                    _isVisible = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool CommandPanelEnabled => !IsBlankScreen && !IsMediaActive;

        public string Title
        {
            get => _title;
            set
            {
                if (_title == null || !_title.Equals(value))
                {
                    _title = value;
                    RaisePropertyChanged();
                }
            }
        }

        public string SortKey
        {
            get
            {
                if (string.IsNullOrEmpty(FilePath))
                {
                    return string.Empty;
                }

                var name = Path.GetFileNameWithoutExtension(FilePath);

                var prefix = name.GetNumericPrefix();
                if (string.IsNullOrEmpty(prefix))
                {
                    return name;
                }

                if (!int.TryParse(prefix, out var prefixNum))
                {
                    return name;
                }

                var remainder = name.Replace(prefix, string.Empty).Trim();
                return $"{prefixNum:D6} {remainder}";
            }
        }

        public string FilePath { get; set; }

        public string FileNameAsSubTitle
        {
            get => _fileNameAsSubTitle;
            set
            {
                if (_fileNameAsSubTitle != value)
                {
                    _fileNameAsSubTitle = value;
                    RaisePropertyChanged();
                }
            }
        }

        public long LastChanged { get; set; }
        
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
                    RaisePropertyChanged(nameof(IsSliderVisible));
                }
            }
        }

        public string PauseIconKind =>
            IsPaused
                ? "Play"
                : "Pause";
        
        public SupportedMediaType MediaType { get; set; }

        public ImageSource ThumbnailImageSource
        {
            get => _thumbnailImageSource;
            set
            {
                if (_thumbnailImageSource == null || !_thumbnailImageSource.Equals(value))
                {
                    _thumbnailImageSource = value;

                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(IsPlayButtonVisible));
                    RaisePropertyChanged(nameof(IsStopButtonVisible));
                    RaisePropertyChanged(nameof(IsPreparingMedia));
                }
            }
        }

        public bool IsPreparingMedia =>
            _thumbnailImageSource == null ||
            (HasDuration && DurationDeciseconds == 0);

        public bool IsPlayButtonVisible => !IsMediaActive && !IsPreparingMedia;

        public bool IsStopButtonVisible => IsMediaActive && !IsPreparingMedia;

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
                    RaisePropertyChanged(nameof(IsPauseButtonVisible));
                    RaisePropertyChanged(nameof(IsPlayButtonVisible));
                    RaisePropertyChanged(nameof(IsStopButtonVisible));
                    RaisePropertyChanged(nameof(IsSliderVisible));
                    RaisePropertyChanged(nameof(PlaybackTimeColorBrush));
                    RaisePropertyChanged(nameof(DurationColorBrush));
                    RaisePropertyChanged(nameof(CommandPanelEnabled));
                    RaisePropertyChanged(nameof(IsPreviousSlideButtonEnabled));
                    RaisePropertyChanged(nameof(IsNextSlideButtonEnabled));
                    RaisePropertyChanged(nameof(SlideshowProgressString));
                }
            }
        }

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

        public int SlideshowCount
        {
            get => _slideshowCount;
            set
            {
                if (_slideshowCount != value)
                {
                    _slideshowCount = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(SlideshowProgressString));
                }
            }
        }

        public bool IsRollingSlideshow
        {
            get => _isRollingSlideshow;
            set
            {
                if (_isRollingSlideshow != value)
                {
                    _isRollingSlideshow = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(SlideshowProgressString));
                }
            }
        }

        public bool SlideshowLoop { get; set; }

        public int CurrentSlideshowIndex
        {
            get => _currentSlideshowIndex;
            set
            {
                if (_currentSlideshowIndex != value)
                {
                    _currentSlideshowIndex = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(IsPreviousSlideButtonEnabled));
                    RaisePropertyChanged(nameof(IsNextSlideButtonEnabled));
                    RaisePropertyChanged(nameof(SlideshowProgressString));
                }
            }
        }
        
        public bool IsPreviousSlideButtonEnabled => 
            MediaType.Classification == MediaClassification.Slideshow && 
            IsMediaActive &&
            (SlideshowLoop || CurrentSlideshowIndex > 0);

        public bool IsNextSlideButtonEnabled => 
            MediaType.Classification == MediaClassification.Slideshow && 
            IsMediaActive &&
            (SlideshowLoop || CurrentSlideshowIndex < SlideshowCount - 1);

        public bool HasDuration =>
            MediaType.Classification == MediaClassification.Audio ||
            MediaType.Classification == MediaClassification.Video;

        public bool HasDurationAndIsPlaying => HasDuration && IsMediaActive && !IsPaused;

        public bool AllowPositionSeeking
        {
            get => _allowPositionSeeking;
            set
            {
                if (_allowPositionSeeking != value)
                {
                    _allowPositionSeeking = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(IsSliderVisible));
                }
            }
        }
        
        public bool AllowPause
        {
            get => _allowPause;
            set
            {
                if (_allowPause != value)
                {
                    _allowPause = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(IsPauseButtonVisible));
                }
            }
        }

        public bool IsPauseButtonVisible => HasDuration && IsMediaActive && AllowPause;

        public bool IsSlideshow => MediaType.Classification == MediaClassification.Slideshow;

        public string MiscText
        {
            get => _miscText;
            set
            {
                if (_miscText != value)
                {
                    _miscText = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool IsSliderVisible => 
            HasDuration && 
            AllowPositionSeeking && 
            (!IsMediaActive || IsPaused);

        public string SlideshowProgressString
        {
            get
            {
                if (!IsSlideshow)
                {
                    return null;
                }

                if (!IsMediaActive)
                {
                    if (SlideshowCount == 1)
                    {
                        return IsRollingSlideshow
                            ? Properties.Resources.CONTAINS_1_ROLLING_SLIDE
                            : Properties.Resources.CONTAINS_1_SLIDE;
                    }

                    return string.Format(
                        IsRollingSlideshow
                            ? Properties.Resources.CONTAINS_X_ROLLING_SLIDES
                            : Properties.Resources.CONTAINS_X_SLIDES,
                        SlideshowCount);
                }

                return string.Format(
                    IsRollingSlideshow
                        ? Properties.Resources.ROLLING_SLIDE_X_OF_Y
                        : Properties.Resources.SLIDE_X_OF_Y, 
                    CurrentSlideshowIndex + 1, 
                    SlideshowCount);
            }
        }

        public int PlaybackPositionDeciseconds
        {
            get => _playbackPositionDeciseconds;
            set
            {
                if (_playbackPositionDeciseconds != value)
                {
                    _playbackPositionDeciseconds = value;

                    PlaybackTimeString = GenerateTimeString(_playbackPositionDeciseconds * 100);

                    RaisePropertyChanged();
                    OnPlaybackPositionChangedEvent();
                }
            }
        }
        
        public string PlaybackTimeString
        {
            get => _playbackTimeString;
            set
            {
                if (!_playbackTimeString.Equals(value))
                {
                    _playbackTimeString = value;
                    RaisePropertyChanged();
                }
            }
        }

        public string DurationString => GenerateTimeString(_durationDeciseconds * 100);
        
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
                    RaisePropertyChanged(nameof(DurationString));
                    RaisePropertyChanged(nameof(IsPreparingMedia));
                    RaisePropertyChanged(nameof(IsPlayButtonVisible));
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

                    case MediaClassification.Slideshow:
                        return SlideshowIconBrush;

                    case MediaClassification.Web:
                        return WebIconBrush;

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

                    case MediaClassification.Slideshow:
                        return "CameraBurst";

                    case MediaClassification.Web:
                        if (MediaType.FileExtension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                        {
                            return "FilePdf";
                        }

                        return "Web";

                    default:
                        return "Question";
                }
            }
        }

        public int CommandPanelBtnColWidth => CommandPanelVisible ? 12 : 0;

        public bool IsImagePopupEnabled
        {
            get
            {
                switch (MediaType.Classification)
                {
                    case MediaClassification.Image:
                    case MediaClassification.Video:
                    case MediaClassification.Slideshow:
                        return true;

                    default:
                        return false;
                }
            }
        }

        private static string GenerateTimeString(long milliseconds)
        {
            return TimeSpan.FromMilliseconds(milliseconds).ToString(@"hh\:mm\:ss");
        }

        private void OnPlaybackPositionChangedEvent()
        {
            PlaybackPositionChangedEvent?.Invoke(this, EventArgs.Empty);
        }
    }
}
