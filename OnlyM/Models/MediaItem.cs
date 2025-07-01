using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using OnlyM.Core.Extensions;
using OnlyM.Core.Models;

namespace OnlyM.Models;

public class MediaItem : ObservableObject
{
    private static readonly SolidColorBrush ImageIconBrush = new(Colors.DarkGreen);
    private static readonly SolidColorBrush AudioIconBrush = new(Colors.CornflowerBlue);
    private static readonly SolidColorBrush VideoIconBrush = new(Colors.Chocolate);
    private static readonly SolidColorBrush SlideshowIconBrush = new(Colors.BlueViolet);
    private static readonly SolidColorBrush WebIconBrush = new(Colors.Firebrick);
    private static readonly SolidColorBrush UnknownIconBrush = new(Colors.Crimson);
    private static readonly SolidColorBrush GreenBrush = new(Colors.DarkGreen);
    private static readonly SolidColorBrush BlackBrush = new(Colors.Black);
    private static readonly SolidColorBrush GrayBrush = new(Colors.DarkGray);

    private bool _isMediaChanging;
    private bool _commandPanelVisible;
    private bool _pauseOnLastFrame;
    private bool _isCommandPanelOpen;
    private bool _allowFreezeCommand;
    private bool _useMirror;
    private bool _isVisible;
    private string? _title;
    private bool _isPaused;
    private ImageSource? _thumbnailImageSource;
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
    private string? _miscText;
    private string? _fileNameAsSubTitle;
    private PdfViewStyle _pdfViewStyle = PdfViewStyle.Default;
    private string _chosenPdfPage = "1";

    public event EventHandler? PlaybackPositionChangedEvent;

    public Guid Id { get; init; }

    public bool IsVideo => MediaType?.Classification == MediaClassification.Video;

    public bool IsWeb => MediaType?.Classification == MediaClassification.Web;

    public bool IsWebAndAllowMirror => IsWeb && AllowUseMirror;

    public bool IsPdf => FilePath != null && Path.GetExtension(FilePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase);

    public bool IsBlankScreen { get; init; }

    public bool UseMirror
    {
        get => _useMirror;
        set => SetProperty(ref _useMirror, value);
    }

    public bool AllowUseMirror
    {
        get => _allowUseMirror;
        set
        {
            if (SetProperty(ref _allowUseMirror, value))
            {
                OnPropertyChanged(nameof(IsWebAndAllowMirror));
            }
        }
    }

    public bool CommandPanelVisible
    {
        get => _commandPanelVisible;
        set
        {
            if (SetProperty(ref _commandPanelVisible, value))
            {
                OnPropertyChanged(nameof(CommandPanelBtnColWidth));

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
        set => SetProperty(ref _pauseOnLastFrame, value);
    }

    public bool AllowFreezeCommand
    {
        get => _allowFreezeCommand;
        set
        {
            if (SetProperty(ref _allowFreezeCommand, value))
            {
                OnPropertyChanged(nameof(ShouldDisplayFreezeCommand));
            }
        }
    }

    public bool ShouldDisplayFreezeCommand => IsVideo && AllowFreezeCommand;

    public bool ShouldDisplayPdfViewCombo => IsPdf;

    public bool ShouldDisplayPdfPageTextBox => IsPdf;

    public static IEnumerable<PdfViewStyleAndDescription> PdfViewStyles =>
    [
        new() { Style = PdfViewStyle.Default, Description = Properties.Resources.PDF_VIEW_STYLE_DEFAULT },
        new() { Style = PdfViewStyle.VerticalFit, Description = Properties.Resources.PDF_VIEW_STYLE_VERT },
        new() { Style = PdfViewStyle.HorizontalFit, Description = Properties.Resources.PDF_VIEW_STYLE_HORZ }
    ];

    public string ChosenPdfPage
    {
        get => _chosenPdfPage;
        set
        {
            if (_chosenPdfPage != value &&
                int.TryParse(value, out var pageNumber) &&
                pageNumber > 0)
            {
                _chosenPdfPage = value;
                OnPropertyChanged();
            }
        }
    }

    public PdfViewStyle ChosenPdfViewStyle
    {
        get => _pdfViewStyle;
        set => SetProperty(ref _pdfViewStyle, value);
    }

    public bool IsCommandPanelOpen
    {
        get => _isCommandPanelOpen;
        set => SetProperty(ref _isCommandPanelOpen, value);
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    public bool CommandPanelEnabled => !IsBlankScreen && !IsMediaActive;

    public string? Title
    {
        get => _title;
        set
        {
            if (_title == null || !_title.Equals(value, StringComparison.Ordinal))
            {
                _title = value;
                OnPropertyChanged();
            }
        }
    }

    public int VideoRotation { get; set; }

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
            if (string.IsNullOrEmpty(prefix) || !int.TryParse(prefix, out var prefixNum))
            {
                return name;
            }

            var remainder = name[prefix.Length..].Trim();
            return $"{prefixNum:D6} {remainder}";
        }
    }

    public string? FilePath { get; init; }

    public string? FileNameAsSubTitle
    {
        get => _fileNameAsSubTitle;
        set => SetProperty(ref _fileNameAsSubTitle, value);
    }

    public long LastChanged { get; set; }

    public bool IsPaused
    {
        get => _isPaused;

        set
        {
            if (SetProperty(ref _isPaused, value))
            {
                OnPropertyChanged(nameof(PauseIconKind));
                OnPropertyChanged(nameof(HasDurationAndIsPlaying));
                OnPropertyChanged(nameof(IsSliderVisible));
                OnPropertyChanged(nameof(IsStartOffsetButtonEnabled));
            }
        }
    }

    public string PauseIconKind =>
        IsPaused
            ? "Play"
            : "Pause";

    public SupportedMediaType? MediaType { get; init; }

    public ImageSource? ThumbnailImageSource
    {
        get => _thumbnailImageSource;
        set
        {
            if (_thumbnailImageSource == null || !_thumbnailImageSource.Equals(value))
            {
                _thumbnailImageSource = value;

                OnPropertyChanged();
                OnPropertyChanged(nameof(IsPlayButtonVisible));
                OnPropertyChanged(nameof(IsStopButtonVisible));
                OnPropertyChanged(nameof(IsPreparingMedia));
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
            if (SetProperty(ref _isMediaActive, value))
            {
                OnPropertyChanged(nameof(HasDurationAndIsPlaying));
                OnPropertyChanged(nameof(IsPauseButtonVisible));
                OnPropertyChanged(nameof(IsPlayButtonVisible));
                OnPropertyChanged(nameof(IsStopButtonVisible));
                OnPropertyChanged(nameof(IsSliderVisible));
                OnPropertyChanged(nameof(PlaybackTimeColorBrush));
                OnPropertyChanged(nameof(DurationColorBrush));
                OnPropertyChanged(nameof(CommandPanelEnabled));
                OnPropertyChanged(nameof(IsPreviousSlideButtonEnabled));
                OnPropertyChanged(nameof(IsNextSlideButtonEnabled));
                OnPropertyChanged(nameof(SlideshowProgressString));
                OnPropertyChanged(nameof(IsStartOffsetButtonEnabled));
            }
        }
    }

    public bool IsMediaChanging
    {
        get => _isMediaChanging;
        set => SetProperty(ref _isMediaChanging, value);
    }

    public bool IsPlayButtonEnabled
    {
        get => _isPlayButtonEnabled;
        set => SetProperty(ref _isPlayButtonEnabled, value);
    }

    public int SlideshowCount
    {
        get => _slideshowCount;
        set
        {
            if (SetProperty(ref _slideshowCount, value))
            {
                OnPropertyChanged(nameof(SlideshowProgressString));
            }
        }
    }

    public bool IsRollingSlideshow
    {
        get => _isRollingSlideshow;
        set
        {
            if (SetProperty(ref _isRollingSlideshow, value))
            {
                OnPropertyChanged(nameof(SlideshowProgressString));
            }
        }
    }

    public bool SlideshowLoop { get; set; }

    public int CurrentSlideshowIndex
    {
        get => _currentSlideshowIndex;
        set
        {
            if (SetProperty(ref _currentSlideshowIndex, value))
            {
                OnPropertyChanged(nameof(IsPreviousSlideButtonEnabled));
                OnPropertyChanged(nameof(IsNextSlideButtonEnabled));
                OnPropertyChanged(nameof(SlideshowProgressString));
            }
        }
    }

    public bool IsPreviousSlideButtonEnabled =>
        MediaType?.Classification == MediaClassification.Slideshow &&
        IsMediaActive &&
        (SlideshowLoop || CurrentSlideshowIndex > 0);

    public bool IsNextSlideButtonEnabled =>
        MediaType?.Classification == MediaClassification.Slideshow &&
        IsMediaActive &&
        (SlideshowLoop || CurrentSlideshowIndex < SlideshowCount - 1);

    public bool HasDuration =>
        MediaType?.Classification == MediaClassification.Audio ||
        MediaType?.Classification == MediaClassification.Video;

    public bool IsStartOffsetButtonVisible => HasDuration && AllowPositionSeeking;

    public bool HasDurationAndIsPlaying => HasDuration && IsMediaActive && !IsPaused;

    public bool IsStartOffsetButtonEnabled => !IsMediaActive || IsPaused;

    public bool AllowPositionSeeking
    {
        get => _allowPositionSeeking;
        set
        {
            if (SetProperty(ref _allowPositionSeeking, value))
            {
                OnPropertyChanged(nameof(IsSliderVisible));
                OnPropertyChanged(nameof(IsStartOffsetButtonVisible));
            }
        }
    }

    public bool AllowPause
    {
        get => _allowPause;
        set
        {
            if (SetProperty(ref _allowPause, value))
            {
                OnPropertyChanged(nameof(IsPauseButtonVisible));
            }
        }
    }

    public bool IsPauseButtonVisible => HasDuration && IsMediaActive && AllowPause;

    public bool IsSlideshow => MediaType?.Classification == MediaClassification.Slideshow;

    public string? MiscText
    {
        get => _miscText;
        set => SetProperty(ref _miscText, value);
    }

    public bool IsSliderVisible =>
        HasDuration &&
        AllowPositionSeeking &&
        (!IsMediaActive || IsPaused);

    public string? SlideshowProgressString
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
                    CultureInfo.CurrentCulture,
                    IsRollingSlideshow ? Properties.Resources.CONTAINS_X_ROLLING_SLIDES : Properties.Resources.CONTAINS_X_SLIDES,
                    SlideshowCount);
            }

            return string.Format(
                CultureInfo.CurrentCulture,
                IsRollingSlideshow ? Properties.Resources.ROLLING_SLIDE_X_OF_Y : Properties.Resources.SLIDE_X_OF_Y,
                CurrentSlideshowIndex + 1,
                SlideshowCount);
        }
    }

    public int PlaybackPositionDeciseconds
    {
        get => _playbackPositionDeciseconds;
        set
        {
            if (SetProperty(ref _playbackPositionDeciseconds, value))
            {
                PlaybackTimeString = GenerateTimeString(_playbackPositionDeciseconds * 100);
                OnPlaybackPositionChangedEvent();
            }
        }
    }

    public string PlaybackTimeString
    {
        get => _playbackTimeString;
        private set
        {
            if (!_playbackTimeString.Equals(value, StringComparison.Ordinal))
            {
                _playbackTimeString = value;
                OnPropertyChanged();
            }
        }
    }

    public string DurationString => GenerateTimeString(_durationDeciseconds * 100);

    public int DurationDeciseconds
    {
        get => _durationDeciseconds;
        set
        {
            if (SetProperty(ref _durationDeciseconds, value))
            {
                OnPropertyChanged(nameof(DurationString));
                OnPropertyChanged(nameof(IsPreparingMedia));
                OnPropertyChanged(nameof(IsPlayButtonVisible));
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
            switch (MediaType?.Classification)
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
            switch (MediaType?.Classification)
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
                    if (MediaType.FileExtension != null && MediaType.FileExtension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
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
            switch (MediaType?.Classification)
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

    private static string GenerateTimeString(int milliseconds) =>
        TimeSpan.FromMilliseconds(milliseconds).AsMediaDurationString();

    private void OnPlaybackPositionChangedEvent() =>
        PlaybackPositionChangedEvent?.Invoke(this, EventArgs.Empty);
}
