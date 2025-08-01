﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using OnlyM.Core.Extensions;
using OnlyM.Core.Models;
using OnlyM.Core.Services.Options;
using OnlyM.Core.Utils;
using OnlyM.CoreSys;
using OnlyM.Models;
using OnlyM.Services.ImagesCache;
using OnlyM.Slides;
using OnlyM.Slides.Models;
using Serilog;
using Serilog.Events;

namespace OnlyM.Services;

internal sealed class ImageDisplayManager
{
    private static readonly ImageCache ImageCache = new();
    private readonly IOptionsService _optionsService;

    private readonly string _slideshowStagingFolder;
    private readonly DispatcherTimer _slideshowTimer = new();
    private readonly object _slideshowLock = new();

    private readonly Image _image1;
    private readonly Image _image2;

    private Guid _image1MediaItemId;
    private Guid _image2MediaItemId;

    private MediaClassification _mediaClassification1;
    private MediaClassification _mediaClassification2;

    private int _currentSlideshowImageIndex;
    private List<Slide>? _slides;
    private bool _shouldLoopSlideshow;
    private Guid _slideshowGuid;
    private bool _autoPlaySlideshow;
    private bool _autoCloseSlideshow;
    private int _autoPlaySlideshowDwellTime;
    private bool _slideshowTransitioning;

    public ImageDisplayManager(Image image1, Image image2, IOptionsService optionsService)
    {
        _optionsService = optionsService;

        _image1 = image1;
        _image2 = image2;

        _image1.Name = "Img1";
        _image2.Name = "Img2";

        _slideshowStagingFolder = FileUtils.GetUsersTempStagingFolder();

        _slideshowTimer.Tick += HandleSlideshowTimerTick;
    }

    public event EventHandler<MediaEventArgs>? MediaChangeEvent;

    public event EventHandler<SlideTransitionEventArgs>? SlideTransitionEvent;

    private bool Image1Populated => _image1.Source != null;

    private bool Image2Populated => _image2.Source != null;

    public void ShowSingleImage(
        string mediaFilePath,
        Guid mediaItemId,
        bool isBlankScreenImage)
    {
        var fadeType = _optionsService.ImageFadeType;

        PlaceImage(
            mediaItemId,
            MediaClassification.Image,
            mediaFilePath,
            isBlankScreenImage,
            fadeType,
            (newMediaId, newMediaClassification) =>
            {
                // starting...
                OnMediaChangeEvent(CreateMediaEventArgs(newMediaId, newMediaClassification, MediaChange.Starting));
            },
            (hiddenMediaId, hiddenMediaClassification) =>
            {
                // stopping...
                OnMediaChangeEvent(CreateMediaEventArgs(hiddenMediaId, hiddenMediaClassification, MediaChange.Stopping));
            },
            (hiddenMediaId, hiddenMediaClassification) =>
            {
                // stopped...
                OnMediaChangeEvent(CreateMediaEventArgs(hiddenMediaId, hiddenMediaClassification, MediaChange.Stopped));
            },
            (newMediaId, newMediaClassification) =>
            {
                // started...
                OnMediaChangeEvent(CreateMediaEventArgs(newMediaId, newMediaClassification, MediaChange.Started));
            });
    }

    public void HideSingleImage(Guid mediaItemId)
    {
        var fadeType = _optionsService.ImageFadeType;
        UnplaceImage(
            mediaItemId,
            MediaClassification.Image,
            fadeType,
            (mediaId, mediaClassification) =>
            {
                // stopping...
                OnMediaChangeEvent(CreateMediaEventArgs(mediaId, mediaClassification, MediaChange.Stopping));
            },
            (mediaId, mediaClassification) =>
            {
                // stopped...
                OnMediaChangeEvent(CreateMediaEventArgs(mediaId, mediaClassification, MediaChange.Stopped));
            });
    }

    public void StartSlideshow(string mediaItemFilePath, Guid mediaItemId)
    {
        lock (_slideshowLock)
        {
            _slideshowTransitioning = false;
            _currentSlideshowImageIndex = 0;

            _slideshowGuid = mediaItemId;

            InitFromSlideshowFile(mediaItemFilePath);

            DisplaySlide(GetCurrentSlide(), mediaItemId, null, 0, _currentSlideshowImageIndex);

            ConfigureSlideshowAutoPlayTimer();
        }
    }

    public int GotoPreviousSlide()
    {
        var oldSlide = GetCurrentSlide();
#pragma warning disable U2U1017
        var oldIndex = _currentSlideshowImageIndex;
#pragma warning restore U2U1017

        --_currentSlideshowImageIndex;

        if (_currentSlideshowImageIndex < 0)
        {
            CheckSlides();
            _currentSlideshowImageIndex = _shouldLoopSlideshow ? _slides!.Count - 1 : 0;
        }

        var newSlide = GetCurrentSlide();

        if (oldSlide != newSlide)
        {
            var mediaId = GetSlideshowMediaId();

            if (mediaId != Guid.Empty)
            {
                DisplaySlide(newSlide, mediaId, oldSlide, oldIndex, _currentSlideshowImageIndex);
            }
        }

        return _currentSlideshowImageIndex;
    }

    public int GotoNextSlide(Action? onTransitionFinished = null)
    {
        var oldSlide = GetCurrentSlide();
#pragma warning disable U2U1017
        var oldIndex = _currentSlideshowImageIndex;
#pragma warning restore U2U1017

        ++_currentSlideshowImageIndex;

        CheckSlides();
        if (_currentSlideshowImageIndex > _slides!.Count - 1)
        {
            _currentSlideshowImageIndex = _shouldLoopSlideshow ? 0 : _slides.Count - 1;
        }

        var newSlide = GetCurrentSlide();

        if (oldSlide != newSlide)
        {
            var mediaId = GetSlideshowMediaId();

            if (mediaId != Guid.Empty)
            {
                DisplaySlide(newSlide, mediaId, oldSlide, oldIndex, _currentSlideshowImageIndex, onTransitionFinished);
            }
            else
            {
                onTransitionFinished?.Invoke();
            }
        }
        else
        {
            onTransitionFinished?.Invoke();
        }

        return _currentSlideshowImageIndex;
    }

    public void StopSlideshow(Guid mediaItemId)
    {
        if (_slideshowTransitioning)
        {
            return;
        }

        _slideshowTimer.Stop();

        UnplaceImage(
            mediaItemId,
            MediaClassification.Slideshow,
            ImageFadeType.FadeOut,
            (mediaId, mediaClassification) =>
            {
                // stopping...
                OnMediaChangeEvent(CreateMediaEventArgs(mediaId, mediaClassification, MediaChange.Stopping));
            },
            (mediaId, mediaClassification) =>
            {
                // stopped...
                OnMediaChangeEvent(CreateMediaEventArgs(mediaId, mediaClassification, MediaChange.Stopped));
            });

        _currentSlideshowImageIndex = 0;
        _slides?.Clear();
    }

    public void CacheImageItem(string mediaFilePath)
    {
        if (_optionsService.CacheImages)
        {
            ImageCache.GetImage(mediaFilePath);
        }
    }

    private void OnMediaChangeEvent(MediaEventArgs? e)
    {
        if (e != null)
        {
            if (Log.Logger.IsEnabled(LogEventLevel.Verbose))
            {
                Log.Logger.Verbose("Media change: {Type}, {Id}", e.Change, e.MediaItemId);
            }

            MediaChangeEvent?.Invoke(this, e);
        }
    }

    private void OnSlideTransitionEvent(SlideTransitionEventArgs e)
    {
        if (Log.Logger.IsEnabled(LogEventLevel.Verbose))
        {
            Log.Logger.Verbose("Slide change: {Type}, {Id}", e.Transition, e.MediaItemId);
        }

        SlideTransitionEvent?.Invoke(this, e);
    }

    private static MediaEventArgs? CreateMediaEventArgs(Guid id, MediaClassification mediaClassification, MediaChange change)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        return new MediaEventArgs
        {
            MediaItemId = id,
            Classification = mediaClassification,
            Change = change,
        };
    }

    private static SlideTransitionEventArgs CreateSlideTransitionEventArgs(Guid id, SlideTransition change, int oldIndex, int newIndex) =>
        new()
        {
            MediaItemId = id,
            Transition = change,
            OldSlideIndex = oldIndex,
            NewSlideIndex = newIndex,
        };

    private void ShowImageInternal(
        bool isBlankScreenImage,
        ScreenPosition screenPosition,
        string imageFile,
        Image controlToUse,
        Image otherControl,
        ImageFadeType fadeType,
        double fadeTime,
        Action hideCompleted,
        Action showCompleted)
    {
        controlToUse.SetValue(Panel.ZIndexProperty, 1);
        otherControl.SetValue(Panel.ZIndexProperty, 0);

        controlToUse.Stretch = isBlankScreenImage
            ? Stretch.Fill
            : Stretch.Uniform;

        ScreenPositionHelper.SetScreenPosition(
            controlToUse,
            isBlankScreenImage ? new ScreenPosition() : screenPosition);

        if (fadeType == ImageFadeType.CrossFade ||
            fadeType == ImageFadeType.None)
        {
            ShowImageInControl(imageFile, controlToUse, fadeType, fadeTime, showCompleted);
            HideImageInControl(otherControl, fadeType, fadeTime, hideCompleted);
        }
        else
        {
            HideImageInControl(
                otherControl,
                fadeType,
                fadeTime,
                () =>
                {
                    hideCompleted.Invoke();
                    ShowImageInControl(imageFile, controlToUse, fadeType, fadeTime, showCompleted);
                });
        }
    }

    private static void RemoveAnimation(Image imageControl)
    {
        imageControl.BeginAnimation(UIElement.OpacityProperty, null);
        imageControl.Opacity = 1.0;
    }

    private static void HideImageInControl(Image imageCtrl, ImageFadeType fadeType, double fadeTime, Action completed)
    {
        var shouldFadeOut = imageCtrl.Source != null &&
                            (fadeType == ImageFadeType.FadeOut ||
                             fadeType == ImageFadeType.FadeInOut ||
                             fadeType == ImageFadeType.CrossFade);

        if (!shouldFadeOut)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                completed.Invoke();
                imageCtrl.Source = null;
                RemoveAnimation(imageCtrl);
            });
        }
        else
        {
            var fadeOut = new DoubleAnimation
            {
                Duration = TimeSpan.FromSeconds(fadeTime),
                From = 1.0,
                To = 0.0,
            };

            fadeOut.Completed += (_, _) =>
            {
                completed.Invoke();
                imageCtrl.Source = null;
            };

            imageCtrl.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }
    }

    private void ShowImageInControl(
        string imageFile, Image imageCtrl, ImageFadeType fadeType, double fadeTime, Action completed)
    {
        var shouldFadeIn =
            fadeType == ImageFadeType.FadeIn ||
            fadeType == ImageFadeType.FadeInOut ||
            fadeType == ImageFadeType.CrossFade;

        var imageSrc = _optionsService.CacheImages
            ? ImageCache.GetImage(imageFile)
            : GraphicsUtils.GetBitmapImage(imageFile, ignoreInternalCache: false);

        if (!shouldFadeIn)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                RemoveAnimation(imageCtrl);
                imageCtrl.Source = imageSrc;
                completed.Invoke();
            });
        }
        else
        {
            imageCtrl.Opacity = 0.0;
            imageCtrl.Source = imageSrc;

            // This delay allows us to accommodate large images without the apparent loss of fade-in animation
            // the first time an image is loaded. There must be a better way!
            Task.Delay(10).ContinueWith(_ =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var fadeIn = new DoubleAnimation
                    {
                        // note that the fade in time is longer than fade out - just seems to look better
                        Duration = TimeSpan.FromSeconds(fadeTime * 1.2),
                        From = 0.0,
                        To = 1.0,
                    };

                    fadeIn.Completed += (_, _) => completed();
                    imageCtrl.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                });
            });
        }
    }

    private void InitFromSlideshowFile(string mediaItemFilePath)
    {
        var sf = new SlideFile(mediaItemFilePath);
        if (sf.SlideCount == 0)
        {
            return;
        }

        sf.ExtractImages(GetStagingFolderForSlideshow());

        _slides = sf.GetSlides(includeBitmapImage: false).ToList();
        _shouldLoopSlideshow = sf.Loop;
        _autoPlaySlideshow = sf.AutoPlay;
        _autoCloseSlideshow = sf.AutoClose;
        _autoPlaySlideshowDwellTime = sf.DwellTimeMilliseconds;
    }

    private string GetStagingFolderForSlideshow()
    {
        var folder = Path.Combine(_slideshowStagingFolder, _slideshowGuid.ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    private void ConfigureSlideshowAutoPlayTimer()
    {
        if (_autoPlaySlideshow)
        {
            var slide = GetCurrentSlide();

            var dwellTimeMillisecs = slide.DwellTimeMilliseconds == 0
                ? _autoPlaySlideshowDwellTime
                : slide.DwellTimeMilliseconds;

            _slideshowTimer.Interval = TimeSpan.FromMilliseconds(dwellTimeMillisecs);
            _slideshowTimer.Start();
        }
    }

    private void HandleSlideshowTimerTick(object? sender, EventArgs e)
    {
        _slideshowTransitioning = true;
        _slideshowTimer.Stop();

        var currentSlideIndex = _currentSlideshowImageIndex;
        var newSlideIndex = GotoNextSlide(() => _slideshowTransitioning = false);

        if (currentSlideIndex == newSlideIndex)
        {
            // reached the end and no looping...
            if (_autoCloseSlideshow)
            {
                StopSlideshow(_slideshowGuid);
            }
        }
        else
        {
            ConfigureSlideshowAutoPlayTimer();
        }
    }

    private void DisplaySlide(
        Slide slide,
        Guid mediaItemId,
        Slide? previousSlide,
        int oldIndex,
        int newIndex,
        Action? onTransitionFinished = null)
    {
        ArgumentNullException.ThrowIfNull(slide);
        ArgumentNullException.ThrowIfNull(slide.ArchiveEntryName);

        var direction = newIndex >= oldIndex ? SlideDirection.Forward : SlideDirection.Reverse;

        var fadeType = GetSlideFadeType(slide, previousSlide, direction);

        var imageFilePath = Path.Combine(GetStagingFolderForSlideshow(), slide.ArchiveEntryName);

        PlaceImage(
            mediaItemId,
            MediaClassification.Slideshow,
            imageFilePath,
            false,
            fadeType,
            (newMediaId, newMediaClassification) =>
            {
                // starting...
                if (previousSlide == null)
                {
                    OnMediaChangeEvent(CreateMediaEventArgs(newMediaId, newMediaClassification, MediaChange.Starting));
                }
                else
                {
                    OnSlideTransitionEvent(CreateSlideTransitionEventArgs(mediaItemId, SlideTransition.Started, oldIndex, newIndex));
                }
            },
            (hiddenMediaId, hiddenMediaClassification) =>
            {
                // stopping...
                if (previousSlide == null)
                {
                    OnMediaChangeEvent(CreateMediaEventArgs(hiddenMediaId, hiddenMediaClassification, MediaChange.Stopping));
                }
            },
            (hiddenMediaId, hiddenMediaClassification) =>
            {
                // stopped...
                if (previousSlide == null)
                {
                    OnMediaChangeEvent(CreateMediaEventArgs(hiddenMediaId, hiddenMediaClassification, MediaChange.Stopped));
                }
            },
            (newMediaId, newMediaClassification) =>
            {
                // started...
                if (previousSlide == null)
                {
                    OnMediaChangeEvent(CreateMediaEventArgs(newMediaId, newMediaClassification, MediaChange.Started));
                }
                else
                {
                    OnSlideTransitionEvent(CreateSlideTransitionEventArgs(mediaItemId, SlideTransition.Finished, oldIndex, newIndex));
                    onTransitionFinished?.Invoke();
                }
            });
    }

    private ImageFadeType GetSlideFadeType(Slide slide, Slide? previousSlide, SlideDirection direction)
    {
        var thisSlideFadeInType = direction == SlideDirection.Forward
            ? slide.FadeInForward ? ImageFadeType.FadeIn : ImageFadeType.None
            : slide.FadeInReverse ? ImageFadeType.FadeIn : ImageFadeType.None;

        if (previousSlide == null)
        {
            // previous image (if any) is a regular image _not_ a slide
            var regularImageFadeType = _optionsService.ImageFadeType;
            switch (regularImageFadeType)
            {
                case ImageFadeType.CrossFade:
                case ImageFadeType.FadeInOut:
                case ImageFadeType.FadeOut:
                    if (thisSlideFadeInType == ImageFadeType.FadeIn)
                    {
                        return ImageFadeType.CrossFade;
                    }

                    return thisSlideFadeInType;

                default:
                    return thisSlideFadeInType;
            }
        }

        var previousSlideFadeOutType = direction == SlideDirection.Forward
            ? previousSlide.FadeOutForward ? ImageFadeType.FadeOut : ImageFadeType.None
            : previousSlide.FadeOutReverse ? ImageFadeType.FadeOut : ImageFadeType.None;

        if (thisSlideFadeInType == ImageFadeType.FadeIn && previousSlideFadeOutType == ImageFadeType.FadeOut)
        {
            return ImageFadeType.CrossFade;
        }

        if (previousSlideFadeOutType == ImageFadeType.FadeOut)
        {
            return ImageFadeType.FadeOut;
        }

        return thisSlideFadeInType;
    }

    private Slide GetCurrentSlide()
    {
        CheckSlides();
        return _slides![_currentSlideshowImageIndex];
    }

    private void PlaceImage(
        Guid mediaItemId,
        MediaClassification mediaClassification,
        string imageFilePath,
        bool isBlankScreenImage,
        ImageFadeType fadeType,
        Action<Guid, MediaClassification> onStarting,
        Action<Guid, MediaClassification> onStopping,
        Action<Guid, MediaClassification> onStopped,
        Action<Guid, MediaClassification> onStarted)
    {
        var fadeTime = _optionsService.ImageFadeSpeed.GetFadeSpeedSeconds();

        onStarting.Invoke(mediaItemId, mediaClassification);

        if (!Image1Populated)
        {
            onStopping.Invoke(_image2MediaItemId, _mediaClassification2);

            ShowImageInternal(
                isBlankScreenImage,
                _optionsService.ImageScreenPosition,
                imageFilePath,
                _image1,
                _image2,
                fadeType,
                fadeTime,
                () =>
                {
                    onStopped.Invoke(_image2MediaItemId, _mediaClassification2);
                    _image2MediaItemId = Guid.Empty;
                    _mediaClassification2 = MediaClassification.Unknown;
                },
                () =>
                {
                    _image1MediaItemId = mediaItemId;
                    _mediaClassification1 = mediaClassification;
                    onStarted.Invoke(_image1MediaItemId, _mediaClassification1);
                });
        }
        else if (!Image2Populated)
        {
            onStopping.Invoke(_image1MediaItemId, _mediaClassification1);

            ShowImageInternal(
                isBlankScreenImage,
                _optionsService.ImageScreenPosition,
                imageFilePath,
                _image2,
                _image1,
                fadeType,
                fadeTime,
                () =>
                {
                    onStopped.Invoke(_image1MediaItemId, _mediaClassification1);
                    _image1MediaItemId = Guid.Empty;
                    _mediaClassification1 = MediaClassification.Unknown;
                },
                () =>
                {
                    _image2MediaItemId = mediaItemId;
                    _mediaClassification2 = mediaClassification;
                    onStarted.Invoke(_image2MediaItemId, _mediaClassification2);
                });
        }
    }

    private void UnplaceImage(
        Guid mediaItemId,
        MediaClassification mediaClassification,
        ImageFadeType fadeType,
        Action<Guid, MediaClassification> stopping,
        Action<Guid, MediaClassification> stopped)
    {
        var fadeTime = _optionsService.ImageFadeSpeed.GetFadeSpeedSeconds();

        stopping.Invoke(mediaItemId, mediaClassification);

        if (_image1MediaItemId == mediaItemId && (int)_image1.GetValue(Panel.ZIndexProperty) == 1)
        {
            HideImageInControl(
                _image1,
                fadeType,
                fadeTime,
                () =>
                {
                    stopped.Invoke(mediaItemId, mediaClassification);
                    _image1MediaItemId = Guid.Empty;
                    _mediaClassification1 = MediaClassification.Unknown;
                });
        }

        if (_image2MediaItemId == mediaItemId && (int)_image2.GetValue(Panel.ZIndexProperty) == 1)
        {
            HideImageInControl(
                _image2,
                fadeType,
                fadeTime,
                () =>
                {
                    stopped.Invoke(mediaItemId, mediaClassification);
                    _image2MediaItemId = Guid.Empty;
                    _mediaClassification2 = MediaClassification.Unknown;
                });
        }
    }

    private Guid GetSlideshowMediaId()
    {
        if (Image1Populated && _mediaClassification1 == MediaClassification.Slideshow)
        {
            return _image1MediaItemId;
        }

        if (Image2Populated && _mediaClassification2 == MediaClassification.Slideshow)
        {
            return _image2MediaItemId;
        }

        return Guid.Empty;
    }

    private void CheckSlides()
    {
        if (_slides == null)
        {
            throw new NotSupportedException("Slides not initialised!");
        }
    }
}
