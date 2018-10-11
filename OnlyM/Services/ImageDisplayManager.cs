namespace OnlyM.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using System.Windows.Media.Animation;
    using System.Windows.Media.Imaging;
    using Core.Models;
    using Core.Services.Options;
    using GalaSoft.MvvmLight.Threading;
    using ImagesCache;
    using Models;
    using OnlyM.Core.Extensions;
    using OnlyM.Core.Utils;
    using OnlyM.Slides;
    using OnlyM.Slides.Models;
    using Serilog;

    internal sealed class ImageDisplayManager
    {
        private static readonly ImageCache ImageCache = new ImageCache();
        private readonly IOptionsService _optionsService;

        private readonly string _slideshowStagingFolder;
        
        private readonly Image _image1;
        private readonly Image _image2;

        private Guid _image1MediaItemId;
        private Guid _image2MediaItemId;

        private MediaClassification _mediaClassification1;
        private MediaClassification _mediaClassification2;

        private int _currentSlideshowImageIndex;
        private List<SlideData> _slides;
        private bool _shouldLoopSlideshow;

        public ImageDisplayManager(Image image1, Image image2, IOptionsService optionsService)
        {
            _optionsService = optionsService;

            _image1 = image1;
            _image2 = image2;

            _slideshowStagingFolder = FileUtils.GetUsersTempStagingFolder();
        }

        public event EventHandler<MediaEventArgs> MediaChangeEvent;

        private bool Image1Populated => _image1.Source != null;

        private bool Image2Populated => _image2.Source != null;

        public void ShowImage(
            string mediaFilePath, 
            Guid mediaItemId, 
            MediaClassification mediaClassification,
            bool isBlankScreenImage)
        {
            var fadeType = _optionsService.Options.ImageFadeType;
            PlaceImage(mediaItemId, mediaClassification, mediaFilePath, isBlankScreenImage, fadeType);
        }

        public void HideImage(Guid mediaItemId)
        {
            var fadeType = _optionsService.Options.ImageFadeType;
            UnplaceImage(mediaItemId, MediaClassification.Image, fadeType);
        }

        public void StartSlideshow(string mediaItemFilePath, Guid mediaItemId)
        {
            _currentSlideshowImageIndex = 0;
            
            InitFromSlideshowFile(mediaItemFilePath);

            DisplaySlide(GetCurrentSlide(), mediaItemId);
        }

        public int GotoPreviousSlide()
        {
            var oldSlide = GetCurrentSlide();

            --_currentSlideshowImageIndex;

            if (_currentSlideshowImageIndex < 0)
            {
                _currentSlideshowImageIndex = _shouldLoopSlideshow ? _slides.Count - 1 : 0;
            }
            
            var newSlide = GetCurrentSlide();

            if (oldSlide != newSlide)
            {
                var mediaId = GetSlideshowMediaId();

                if (mediaId != Guid.Empty)
                {
                    DisplaySlide(newSlide, mediaId, oldSlide);
                }
            }

            return _currentSlideshowImageIndex;
        }

        public int GotoNextSlide()
        {
            var oldSlide = GetCurrentSlide();

            ++_currentSlideshowImageIndex;

            if (_currentSlideshowImageIndex > _slides.Count - 1)
            {
                _currentSlideshowImageIndex = _shouldLoopSlideshow ? 0 : _slides.Count - 1;
            }

            var newSlide = GetCurrentSlide();

            if (oldSlide != newSlide)
            {
                var mediaId = GetSlideshowMediaId();

                if (mediaId != Guid.Empty)
                {
                    DisplaySlide(newSlide, mediaId, oldSlide);
                }
            }

            return _currentSlideshowImageIndex;
        }

        public void StopSlideshow(Guid mediaItemId)
        {
            var currentSlide = GetCurrentSlide();

            var fadeType = currentSlide.FadeOut ? ImageFadeType.FadeOut : ImageFadeType.None;
            UnplaceImage(mediaItemId, MediaClassification.Slideshow, fadeType);

            _currentSlideshowImageIndex = 0;
            _slides.Clear();
        }

        public void CacheImageItem(string mediaFilePath)
        {
            if (_optionsService.Options.CacheImages)
            {
                ImageCache.GetImage(mediaFilePath);
            }
        }

        private void OnMediaChangeEvent(MediaEventArgs e)
        {
            Log.Logger.Verbose("Media change: {Type}, {Id}", e.Change, e.MediaItemId);

            MediaChangeEvent?.Invoke(this, e);
        }

        private MediaEventArgs CreateMediaEventArgs(Guid id, MediaClassification mediaClassification, MediaChange change)
        {
            return new MediaEventArgs
            {
                MediaItemId = id,
                Classification = mediaClassification,
                Change = change
            };
        }
        
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

            if (fadeType == ImageFadeType.CrossFade)
            {
                HideImageInControl(otherControl, fadeType, fadeTime, hideCompleted);
                ShowImageInControl(imageFile, controlToUse, fadeType, fadeTime, showCompleted);
            }
            else
            {
                HideImageInControl(
                    otherControl,
                    fadeType,
                    fadeTime,
                    () =>
                    {
                        hideCompleted?.Invoke();
                        ShowImageInControl(imageFile, controlToUse, fadeType, fadeTime, showCompleted);
                    });
            }
        }

        private void HideImageInControl(Image imageCtrl, ImageFadeType fadeType, double fadeTime, Action completed)
        {
            var shouldFadeOut = imageCtrl.Source != null &&
                                (fadeType == ImageFadeType.FadeOut ||
                                 fadeType == ImageFadeType.FadeInOut ||
                                 fadeType == ImageFadeType.CrossFade);

            var fadeOut = new DoubleAnimation
            {
                Duration = TimeSpan.FromSeconds(shouldFadeOut ? fadeTime : 0.001),
                From = shouldFadeOut ? 1.0 : 0.0,
                To = 0.0
            };

            fadeOut.Completed += (sender, args) =>
            {
                completed?.Invoke();
                imageCtrl.Source = null;
            };

            imageCtrl.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        private void ShowImageInControl(string imageFile, Image imageCtrl, ImageFadeType fadeType, double fadeTime, Action completed)
        {
            var shouldFadeIn =
                fadeType == ImageFadeType.FadeIn ||
                fadeType == ImageFadeType.FadeInOut ||
                fadeType == ImageFadeType.CrossFade;

            imageCtrl.Opacity = 0.0;

            imageCtrl.Source = _optionsService.Options.CacheImages
                ? ImageCache.GetImage(imageFile)
                : GetBitmapImageWithCacheOnLoad(imageFile);

            // This delay allows us to accommodate large images without the apparent loss of fade-in animation
            // the first time an image is loaded. There must be a better way!
            Task.Delay(10).ContinueWith(t =>
            {
                DispatcherHelper.CheckBeginInvokeOnUI(() =>
                {
                    var fadeIn = new DoubleAnimation
                    {
                        // note that the fade in time is longer than fade out - just seems to look better
                        Duration = TimeSpan.FromSeconds(shouldFadeIn ? fadeTime * 1.2 : 0.001),
                        From = shouldFadeIn ? 0.0 : 1.0,
                        To = 1.0
                    };

                    if (completed != null)
                    {
                        fadeIn.Completed += (sender, args) => { completed(); };
                    }

                    imageCtrl.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                });
            }).ConfigureAwait(false);
        }

        private BitmapImage GetBitmapImageWithCacheOnLoad(string imageFile)
        {
            var bmp = new BitmapImage();

            bmp.BeginInit();
            bmp.UriSource = new Uri(imageFile);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();

            return bmp;
        }

        private void InitFromSlideshowFile(string mediaItemFilePath)
        {
            var sf = new SlideFile(mediaItemFilePath);
            if (sf.SlideCount == 0)
            {
                return;
            }

            sf.ExtractImages(_slideshowStagingFolder);

            _slides = sf.GetSlides(includeBitmapImage: false).ToList();
            _shouldLoopSlideshow = sf.Loop;
        }

        private void DisplaySlide(SlideData slide, Guid mediaItemId, SlideData previousSlide = null)
        {
            OnMediaChangeEvent(CreateMediaEventArgs(mediaItemId, MediaClassification.Slideshow, MediaChange.Starting));
            
            var fadeType = GetSlideFadeType(slide, previousSlide);
            
            var imageFilePath = Path.Combine(_slideshowStagingFolder, slide.ArchiveEntryName);

            PlaceImage(
                mediaItemId,
                MediaClassification.Slideshow,
                imageFilePath,
                false,
                fadeType);
        }

        private ImageFadeType GetSlideFadeType(SlideData slide, SlideData previousSlide)
        {
            var thisSlideFadeType = slide.FadeIn ? ImageFadeType.FadeIn : ImageFadeType.None;

            if (previousSlide == null)
            {
                // previous image (if any) is a regular image _not_ a slide
                var regularImageFadeType = _optionsService.Options.ImageFadeType;
                switch (regularImageFadeType)
                {
                    case ImageFadeType.CrossFade:
                    case ImageFadeType.FadeInOut:
                    case ImageFadeType.FadeOut:
                        if (slide.FadeIn)
                        {
                            return ImageFadeType.CrossFade;
                        }

                        return thisSlideFadeType;

                    default:
                        return thisSlideFadeType;
                }
            }

            if (slide.FadeIn && previousSlide.FadeOut)
            {
                return ImageFadeType.CrossFade;
            }

            return thisSlideFadeType;
        }

        private SlideData GetCurrentSlide()
        {
            return _slides[_currentSlideshowImageIndex];
        }

        private void PlaceImage(
            Guid mediaItemId, 
            MediaClassification mediaClassification, 
            string imageFilePath,
            bool isBlankScreenImage,
            ImageFadeType fadeType)
        {
            var mustHide = !Image1Populated
                ? Image2Populated && _image2MediaItemId != mediaItemId
                : Image1Populated && _image1MediaItemId != mediaItemId;

            var fadeTime = _optionsService.Options.ImageFadeSpeed.GetFadeSpeedSeconds();

            if (!Image1Populated)
            {
                OnMediaChangeEvent(CreateMediaEventArgs(mediaItemId, mediaClassification, MediaChange.Starting));

                if (mustHide)
                {
                    OnMediaChangeEvent(CreateMediaEventArgs(_image2MediaItemId, _mediaClassification2, MediaChange.Stopping));
                }

                ShowImageInternal(
                    isBlankScreenImage,
                    _optionsService.Options.ImageScreenPosition,
                    imageFilePath,
                    _image1,
                    _image2,
                    fadeType,
                    fadeTime,
                    () =>
                    {
                        if (mustHide)
                        {
                            OnMediaChangeEvent(CreateMediaEventArgs(_image2MediaItemId, _mediaClassification2, MediaChange.Stopped));
                            _image2MediaItemId = Guid.Empty;
                            _mediaClassification2 = MediaClassification.Unknown;
                        }
                    },
                    () =>
                    {
                        _image1MediaItemId = mediaItemId;
                        _mediaClassification1 = mediaClassification;
                        OnMediaChangeEvent(CreateMediaEventArgs(mediaItemId, mediaClassification, MediaChange.Started));
                    });
            }
            else if (!Image2Populated)
            {
                OnMediaChangeEvent(CreateMediaEventArgs(mediaItemId, mediaClassification, MediaChange.Starting));

                if (mustHide)
                {
                    OnMediaChangeEvent(CreateMediaEventArgs(_image1MediaItemId, _mediaClassification1, MediaChange.Stopping));
                }

                ShowImageInternal(
                    isBlankScreenImage,
                    _optionsService.Options.ImageScreenPosition,
                    imageFilePath,
                    _image2,
                    _image1,
                    fadeType,
                    fadeTime,
                    () =>
                    {
                        if (mustHide)
                        {
                            OnMediaChangeEvent(CreateMediaEventArgs(_image1MediaItemId, _mediaClassification1, MediaChange.Stopped));
                            _image1MediaItemId = Guid.Empty;
                            _mediaClassification1 = MediaClassification.Unknown;
                        }
                    },
                    () =>
                    {
                        _image2MediaItemId = mediaItemId;
                        _mediaClassification2 = mediaClassification;
                        OnMediaChangeEvent(CreateMediaEventArgs(mediaItemId, mediaClassification, MediaChange.Started));
                    });
            }
        }

        private void UnplaceImage(
            Guid mediaItemId, 
            MediaClassification mediaClassification,
            ImageFadeType fadeType)
        {
            var fadeTime = _optionsService.Options.ImageFadeSpeed.GetFadeSpeedSeconds();

            OnMediaChangeEvent(CreateMediaEventArgs(mediaItemId, mediaClassification, MediaChange.Stopping));

            if (_image1MediaItemId == mediaItemId)
            {
                if ((int)_image1.GetValue(Panel.ZIndexProperty) == 1)
                {
                    HideImageInControl(
                        _image1,
                        fadeType,
                        fadeTime,
                        () =>
                        {
                            OnMediaChangeEvent(CreateMediaEventArgs(mediaItemId, mediaClassification, MediaChange.Stopped));
                            _image1MediaItemId = Guid.Empty;
                            _mediaClassification1 = MediaClassification.Unknown;
                        });
                }
            }

            if (_image2MediaItemId == mediaItemId)
            {
                if ((int)_image2.GetValue(Panel.ZIndexProperty) == 1)
                {
                    OnMediaChangeEvent(CreateMediaEventArgs(mediaItemId, mediaClassification, MediaChange.Stopping));
                    HideImageInControl(
                        _image2,
                        fadeType,
                        fadeTime,
                        () =>
                        {
                            OnMediaChangeEvent(CreateMediaEventArgs(mediaItemId, mediaClassification, MediaChange.Stopped));
                            _image2MediaItemId = Guid.Empty;
                            _mediaClassification2 = MediaClassification.Unknown;
                        });
                }
            }
        }

        private Guid GetSlideshowMediaId()
        {
            var mediaItemId = Guid.Empty;
            if (Image1Populated && _mediaClassification1 == MediaClassification.Slideshow)
            {
                mediaItemId = _image1MediaItemId;
            }
            else if (Image2Populated && _mediaClassification2 == MediaClassification.Slideshow)
            {
                mediaItemId = _image2MediaItemId;
            }

            return mediaItemId;
        }
    }
}
