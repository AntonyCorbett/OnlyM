namespace OnlyM.Services
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media.Animation;
    using System.Windows.Media.Imaging;
    using Core.Models;
    using Core.Services.Options;
    using ImagesCache;
    using Models;
    using Serilog;

    internal sealed class ImageDisplayManager
    {
        private readonly ImageCache _imageCache = new ImageCache();
        private readonly IOptionsService _optionsService;

        private readonly Image _image1;
        private readonly Image _image2;

        private Guid _image1MediaItemId;
        private Guid _image2MediaItemId;

        public event EventHandler<MediaEventArgs> MediaChangeEvent;

        public ImageDisplayManager(Image image1, Image image2, IOptionsService optionsService)
        {
            _image1 = image1;
            _image2 = image2;

            _optionsService = optionsService;
        }

        public ImageFadeType ImageFadeType { private get; set; }

        public FadeSpeed ImageFadeSpeed { private get; set; }

        public void ShowImage(string mediaFilePath, Guid mediaItemId)
        {
            OnMediaChangeEvent(new MediaEventArgs { MediaItemId = mediaItemId, Change = MediaChange.Starting });

            bool mustHide = !Image1Populated
                ? Image2Populated
                : Image1Populated;

            if (!Image1Populated)
            {
                if (mustHide)
                {
                    OnMediaChangeEvent(new MediaEventArgs { MediaItemId = _image2MediaItemId, Change = MediaChange.Stopping });
                }

                ShowImageInternal(
                    mediaFilePath, 
                    _image1,
                    _image2, 
                    () => 
                    {
                        if (mustHide)
                        {
                            OnMediaChangeEvent(new MediaEventArgs { MediaItemId = _image2MediaItemId, Change = MediaChange.Stopped });
                            _image2MediaItemId = Guid.Empty;
                        }
                    }, 
                    () =>
                    {
                        _image1MediaItemId = mediaItemId;
                        OnMediaChangeEvent(new MediaEventArgs { MediaItemId = mediaItemId, Change = MediaChange.Started });
                    });
            }
            else if (!Image2Populated)
            {
                if (mustHide)
                {
                    OnMediaChangeEvent(new MediaEventArgs { MediaItemId = _image1MediaItemId, Change = MediaChange.Stopping });
                }

                ShowImageInternal(
                    mediaFilePath,
                    _image2, 
                    _image1, 
                    () =>
                    {
                        if (mustHide)
                        {
                            OnMediaChangeEvent(new MediaEventArgs { MediaItemId = _image1MediaItemId, Change = MediaChange.Stopped });
                            _image1MediaItemId = Guid.Empty;
                        }
                    }, 
                    () =>
                    {
                        _image2MediaItemId = mediaItemId;
                        OnMediaChangeEvent(new MediaEventArgs { MediaItemId = mediaItemId, Change = MediaChange.Started });
                    });
            }
        }

        public void HideImage(Guid mediaItemId)
        {
            if (_image1MediaItemId == mediaItemId)
            {
                OnMediaChangeEvent(new MediaEventArgs { MediaItemId = mediaItemId, Change = MediaChange.Stopping });

                HideImageInControl(
                    _image1, 
                    () =>
                    {
                        OnMediaChangeEvent(new MediaEventArgs { MediaItemId = mediaItemId, Change = MediaChange.Stopped });
                        _image1MediaItemId = Guid.Empty;
                    });
            }
            else if (_image2MediaItemId == mediaItemId)
            {
                OnMediaChangeEvent(new MediaEventArgs { MediaItemId = mediaItemId, Change = MediaChange.Stopping });

                HideImageInControl(
                    _image2,
                    () =>
                    {
                        OnMediaChangeEvent(new MediaEventArgs { MediaItemId = mediaItemId, Change = MediaChange.Stopped });
                        _image2MediaItemId = Guid.Empty;
                    });
            }
        }

        public void CacheImageItem(string mediaFilePath)
        {
            if (_optionsService.Options.CacheImages)
            {
                _imageCache.GetImage(mediaFilePath);
            }
        }

        private void ShowImageInternal(
            string imageFile,
            Image controlToUse,
            Image otherControl,
            Action hideCompleted,
            Action showCompleted)
        {
            controlToUse.SetValue(Panel.ZIndexProperty, 1);
            otherControl.SetValue(Panel.ZIndexProperty, 0);

            if (ImageFadeType == ImageFadeType.CrossFade)
            {
                HideImageInControl(otherControl, hideCompleted);
                ShowImageInControl(imageFile, controlToUse, showCompleted);
            }
            else
            {
                HideImageInControl(
                    otherControl, 
                    () =>
                    {
                        hideCompleted();
                        ShowImageInControl(imageFile, controlToUse, showCompleted);
                    });
            }
        }

        private double FadeTime
        {
            get
            {
                switch (ImageFadeSpeed)
                {
                    case FadeSpeed.Slow:
                        return 2.0;

                    case FadeSpeed.Fast:
                        return 0.75;

                    case FadeSpeed.SuperFast:
                        return 0.2;

                    default:
                    case FadeSpeed.Normal:
                        return 1.0;
                }
            }
        }

        private void HideImageInControl(Image imageCtrl, Action completed)
        {
            var shouldFadeOut = imageCtrl.Source != null &&
                (ImageFadeType == ImageFadeType.FadeOut ||
                ImageFadeType == ImageFadeType.FadeInOut ||
                ImageFadeType == ImageFadeType.CrossFade);

            var fadeOut = new DoubleAnimation
            {
                Duration = TimeSpan.FromSeconds(shouldFadeOut ? FadeTime : 0.001),
                From = shouldFadeOut ? 1.0 : 0.0,
                To = 0.0
            };

            fadeOut.Completed += (sender, args) =>
            {
                completed();
                imageCtrl.Source = null;
            };

            imageCtrl.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        private void ShowImageInControl(string imageFile, Image imageCtrl, Action completed)
        {
            var shouldFadeIn =
                ImageFadeType == ImageFadeType.FadeIn ||
                ImageFadeType == ImageFadeType.FadeInOut ||
                ImageFadeType == ImageFadeType.CrossFade;
            
            imageCtrl.Source = _optionsService.Options.CacheImages
                ? _imageCache.GetImage(imageFile)
                : new BitmapImage(new Uri(imageFile));

            var fadeIn = new DoubleAnimation
            {
                Duration = TimeSpan.FromSeconds(shouldFadeIn ? FadeTime : 0.001),
                From = shouldFadeIn ? 0.0 : 1.0,
                To = 1.0
            };

            fadeIn.Completed += (sender, args) => { completed(); };

            imageCtrl.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        }
        
        private void OnMediaChangeEvent(MediaEventArgs e)
        {
            Log.Logger.Verbose("Media change: {Type}, {Id}", e.Change, e.MediaItemId);

            MediaChangeEvent?.Invoke(this, e);
        }

        private bool Image1Populated => _image1.Source != null;

        private bool Image2Populated => _image2.Source != null;
    }
}
