namespace OnlyM.Services
{
    using System;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using System.Windows.Media.Animation;
    using System.Windows.Media.Imaging;
    using GalaSoft.MvvmLight.Threading;
    using OnlyM.Core.Models;
    using OnlyM.Core.Services.Options;
    using OnlyM.Services.ImagesCache;

    internal class ImageControlHelper
    {
        private static readonly ImageCache ImageCache = new ImageCache();
        private readonly IOptionsService _optionsService;

        public ImageControlHelper(IOptionsService optionsService)
        {
            _optionsService = optionsService;
        }

        public void ShowImage(
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

        public void HideImageInControl(Image imageCtrl, ImageFadeType fadeType, double fadeTime, Action completed)
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

        public void CacheImageItem(string mediaFilePath)
        {
            if (_optionsService.Options.CacheImages)
            {
                ImageCache.GetImage(mediaFilePath);
            }
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
    }
}
