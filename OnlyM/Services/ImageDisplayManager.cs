namespace OnlyM.Services
{
    using System;
    using System.Windows.Controls;
    using Core.Models;
    using Core.Services.Options;
    using ImagesCache;
    using Models;
    using OnlyM.Core.Extensions;
    using Serilog;

    internal sealed class ImageDisplayManager
    {
        private readonly ImageControlHelper _imageControlHelper;
        private readonly IOptionsService _optionsService;

        private readonly Image _image1;
        private readonly Image _image2;

        private Guid _image1MediaItemId;
        private Guid _image2MediaItemId;

        public ImageDisplayManager(Image image1, Image image2, IOptionsService optionsService)
        {
            _optionsService = optionsService;

            _image1 = image1;
            _image2 = image2;

            _imageControlHelper = new ImageControlHelper(optionsService);
        }

        public event EventHandler<MediaEventArgs> MediaChangeEvent;

        public ImageFadeType ImageFadeType { private get; set; }

        public FadeSpeed ImageFadeSpeed { private get; set; }

        private bool Image1Populated => _image1.Source != null;

        private bool Image2Populated => _image2.Source != null;

        private double FadeTime => ImageFadeSpeed.GetFadeSpeedSeconds();
        
        public void ShowImage(
            string mediaFilePath, 
            Guid mediaItemId, 
            bool isBlankScreenImage)
        {
            OnMediaChangeEvent(CreateMediaEventArgs(mediaItemId, MediaChange.Starting));
        
            var mustHide = !Image1Populated
                ? Image2Populated
                : Image1Populated;

            if (!Image1Populated)
            {
                if (mustHide)
                {
                    OnMediaChangeEvent(CreateMediaEventArgs(_image2MediaItemId, MediaChange.Stopping));
                }

                _imageControlHelper.ShowImage(
                    isBlankScreenImage,
                    _optionsService.Options.ImageScreenPosition,
                    mediaFilePath, 
                    _image1,
                    _image2, 
                    ImageFadeType,
                    FadeTime,
                    () => 
                    {
                        if (mustHide)
                        {
                            OnMediaChangeEvent(CreateMediaEventArgs(_image2MediaItemId, MediaChange.Stopped));
                            _image2MediaItemId = Guid.Empty;
                        }
                    }, 
                    () =>
                    {
                        _image1MediaItemId = mediaItemId;
                        OnMediaChangeEvent(CreateMediaEventArgs(mediaItemId, MediaChange.Started));
                    });
            }
            else if (!Image2Populated)
            {
                if (mustHide)
                {
                    OnMediaChangeEvent(CreateMediaEventArgs(_image1MediaItemId, MediaChange.Stopping));
                }

                _imageControlHelper.ShowImage(
                    isBlankScreenImage,
                    _optionsService.Options.ImageScreenPosition,
                    mediaFilePath,
                    _image2, 
                    _image1, 
                    ImageFadeType,
                    FadeTime,
                    () =>
                    {
                        if (mustHide)
                        {
                            OnMediaChangeEvent(CreateMediaEventArgs(_image1MediaItemId, MediaChange.Stopped));
                            _image1MediaItemId = Guid.Empty;
                        }
                    }, 
                    () =>
                    {
                        _image2MediaItemId = mediaItemId;
                        OnMediaChangeEvent(CreateMediaEventArgs(mediaItemId, MediaChange.Started));
                    });
            }
        }

        public void HideImage(Guid mediaItemId)
        {
            if (_image1MediaItemId == mediaItemId)
            {
                OnMediaChangeEvent(CreateMediaEventArgs(mediaItemId, MediaChange.Stopping));
                _imageControlHelper.HideImageInControl(
                    _image1, 
                    ImageFadeType,
                    FadeTime,
                    () =>
                    {
                        OnMediaChangeEvent(CreateMediaEventArgs(mediaItemId, MediaChange.Stopped));
                        _image1MediaItemId = Guid.Empty;
                    });
            }
            else if (_image2MediaItemId == mediaItemId)
            {
                OnMediaChangeEvent(CreateMediaEventArgs(mediaItemId, MediaChange.Stopping));
                _imageControlHelper.HideImageInControl(
                    _image2,
                    ImageFadeType,
                    FadeTime,
                    () =>
                    {
                        OnMediaChangeEvent(CreateMediaEventArgs(mediaItemId, MediaChange.Stopped));
                        _image2MediaItemId = Guid.Empty;
                    });
            }
        }

        public void CacheImageItem(string mediaFilePath)
        {
            _imageControlHelper.CacheImageItem(mediaFilePath);
        }

        private void OnMediaChangeEvent(MediaEventArgs e)
        {
            Log.Logger.Verbose("Media change: {Type}, {Id}", e.Change, e.MediaItemId);

            MediaChangeEvent?.Invoke(this, e);
        }

        private MediaEventArgs CreateMediaEventArgs(Guid id, MediaChange change)
        {
            return new MediaEventArgs
            {
                MediaItemId = id,
                Classification = MediaClassification.Image,
                Change = change
            };
        }
    }
}
