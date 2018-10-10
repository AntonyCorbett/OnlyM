namespace OnlyM.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Windows.Controls;
    using Core.Services.Options;
    using OnlyM.Core.Extensions;
    using OnlyM.Core.Models;
    using OnlyM.Core.Utils;
    using OnlyM.Models;
    using OnlyM.Slides;
    using OnlyM.Slides.Models;
    using Serilog;

    internal sealed class SlideshowDisplayManager
    {
        private readonly IOptionsService _optionsService;
        private readonly ImageControlHelper _imageControlHelper;
        private readonly Image _image1;
        private readonly Image _image2;
        private readonly double _fadeTime;
        private readonly string _stagingFolder;
        private Guid _mediaItemId;
        
        private int _currentImageIndex;
        private List<SlideData> _slides;
        
        public SlideshowDisplayManager(Image image1, Image image2, IOptionsService optionsService)
        {
            _optionsService = optionsService;

            _image1 = image1;
            _image2 = image2;

            _fadeTime = optionsService.Options.ImageFadeSpeed.GetFadeSpeedSeconds();

            _stagingFolder = FileUtils.GetUsersTempStagingFolder();

            _imageControlHelper = new ImageControlHelper(optionsService);
        }

        public event EventHandler<MediaEventArgs> MediaChangeEvent;

        private bool Image1Populated => _image1.Source != null;

        private bool Image2Populated => _image2.Source != null;

        public void Start(string mediaItemFilePath, Guid mediaItemId)
        {
            _mediaItemId = mediaItemId;
            _currentImageIndex = 0;

            InitFromSlideshowFile(mediaItemFilePath);
            
            DisplayCurrentSlide();
        }

        public void Next()
        {
            ++_currentImageIndex;
            DisplayCurrentSlide();
        }

        private void DisplayCurrentSlide()
        {
            DisplaySlide(_slides[_currentImageIndex]);
        }

        private void OnMediaChangeEvent(MediaEventArgs e)
        {
            Log.Logger.Verbose("Media change: {Type}, {Id}", e.Change, e.MediaItemId);

            MediaChangeEvent?.Invoke(this, e);
        }

        private MediaEventArgs CreateMediaEventArgs(MediaChange change)
        {
            return new MediaEventArgs
            {
                MediaItemId = _mediaItemId,
                Classification = MediaClassification.Slideshow,
                Change = change
            };
        }

        private void DisplaySlide(SlideData slide)
        {
            OnMediaChangeEvent(CreateMediaEventArgs(MediaChange.Starting));

            var fadeType = slide.FadeIn ? ImageFadeType.FadeIn : ImageFadeType.None;

            var imageFilePath = Path.Combine(_stagingFolder, slide.ArchiveEntryName);

            if (!Image1Populated)
            {
                _imageControlHelper.ShowImage(
                    false,
                    _optionsService.Options.ImageScreenPosition,
                    imageFilePath,
                    _image1,
                    _image2,
                    fadeType,
                    _fadeTime,
                    null,
                    () => { OnMediaChangeEvent(CreateMediaEventArgs(MediaChange.Started)); });
            }
            else if (!Image2Populated)
            {
                _imageControlHelper.ShowImage(
                    false,
                    _optionsService.Options.ImageScreenPosition,
                    imageFilePath,
                    _image2,
                    _image1,
                    fadeType,
                    _fadeTime,
                    null,
                    () => { OnMediaChangeEvent(CreateMediaEventArgs(MediaChange.Started)); });
            }
        }

        private void InitFromSlideshowFile(string mediaItemFilePath)
        {
            var sf = new SlideFile(mediaItemFilePath);
            if (sf.SlideCount == 0)
            {
                return;
            }

            sf.ExtractImages(_stagingFolder);

            _slides = sf.GetSlides(includeBitmapImage: false).ToList();
        }
    }
}
