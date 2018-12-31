using System.Linq;

namespace OnlyM.Slides
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Windows.Media.Imaging;
    using Newtonsoft.Json;
    using OnlyM.Slides.Models;

    public class SlideFileBuilder
    {
        private const string ConfigEntryName = @"config.json";
        private readonly SlidesConfig _config = new SlidesConfig();

        /// <summary>
        /// Initializes a new instance of the <see cref="SlideFileBuilder"/> class.
        /// </summary>
        /// <remarks>Use when creating a slideshow from scratch.</remarks>
        public SlideFileBuilder()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SlideFileBuilder"/> class from an existing slideshow.
        /// </summary>
        /// <param name="slideshowPath">An existing slideshow path.</param>
        public SlideFileBuilder(string slideshowPath)
        {
            if (string.IsNullOrEmpty(slideshowPath))
            {
                return;
            }
            
            var f = new SlideFile(slideshowPath);
            AutoPlay = f.AutoPlay;
            DwellTimeMilliseconds = f.DwellTimeMilliseconds;
            Loop = f.Loop;

            foreach (var slide in f.GetSlides(true))
            {
                _config.Slides.Add(slide);
            }
        }

        public bool AutoPlay
        {
            get => _config.AutoPlay;
            set => _config.AutoPlay = value;
        }

        public int DwellTimeMilliseconds
        {
            get => _config.DwellTimeMilliseconds;
            set => _config.DwellTimeMilliseconds = value;
        }

        public bool Loop
        {
            get => _config.Loop;
            set => _config.Loop = value;
        }

        public string CreateSignature()
        {
            return _config.CreateSignature();
        }

        public IReadOnlyCollection<Slide> GetSlides()
        {
            return _config.Slides;
        }

        public void SyncSlideOrder(IEnumerable<string> slideNames)
        {
            _config.SyncSlideOrder(slideNames);
        }

        public void AddSlide(
            string bitmapPath, 
            bool fadeInForward,
            bool fadeInReverse,
            bool fadeOutForward,
            bool fadeOutReverse,
            int dwellTimeMilliseconds = 0)
        {
            if (!File.Exists(bitmapPath))
            {
                throw new ArgumentException("Could not find file", nameof(bitmapPath));
            }

            var archiveEntryName = Path.GetFileName(bitmapPath);
            if (string.IsNullOrEmpty(archiveEntryName))
            {
                throw new ArgumentException("Could not extract archive entry name", nameof(bitmapPath));
            }

            var slide = new Slide
            {
                ArchiveEntryName = archiveEntryName,
                OriginalFilePath = bitmapPath,
                FadeInForward = fadeInForward,
                FadeInReverse = fadeInReverse,
                FadeOutForward = fadeOutForward,
                FadeOutReverse = fadeOutReverse,
                DwellTimeMilliseconds = dwellTimeMilliseconds
            };

            _config.Slides.Add(slide);
        }

        public Slide GetSlide(string itemName)
        {
            return _config.Slides.SingleOrDefault(x => x.ArchiveEntryName.Equals(itemName, StringComparison.Ordinal));
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times", Justification = "trust streams behave properly")]
        public void Build(string path, bool overwrite)
        {
            if (File.Exists(path) && !overwrite)
            {
                throw new Exception("File already exists!");
            }

            using (var memoryStream = new MemoryStream())
            {
                using (var zip = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
                {
                    var configEntry = zip.CreateEntry(ConfigEntryName);

                    var serializer = new JsonSerializer { Formatting = Formatting.Indented };

                    using (var entryStream = configEntry.Open())
                    using (var entryStreamWriter = new StreamWriter(entryStream))
                    using (var jsonTextWriter = new JsonTextWriter(entryStreamWriter))
                    {
                        serializer.Serialize(jsonTextWriter, _config);
                    }

                    foreach (var slide in _config.Slides)
                    {
                        if (slide.Image != null)
                        {
                            // already have image data...

                            // This is a little odd (multiple streams and rewinding the memory stream!).
                            // I can't find a better way of saving a BitmapSource in the archive entry.
                            BitmapEncoder encoder = new PngBitmapEncoder();
                            encoder.Frames.Add(BitmapFrame.Create(slide.Image));

                            using (var ms = new MemoryStream())
                            {
                                encoder.Save(ms);
                                ms.Seek(0, SeekOrigin.Begin);

                                var entry = zip.CreateEntry(slide.ArchiveEntryName);
                                using (var entryStream = entry.Open())
                                {
                                    ms.CopyTo(entryStream);
                                }
                            }
                        }
                        else
                        {
                            if (!File.Exists(slide.OriginalFilePath))
                            {
                                throw new Exception($"Could not find image file: {slide.OriginalFilePath}");
                            }

                            zip.CreateEntryFromFile(slide.OriginalFilePath, slide.ArchiveEntryName);
                        }
                    }
                }

                if (overwrite)
                {
                    File.Delete(path);
                }

                using (var fileStream = new FileStream(path, FileMode.Create))
                {
                    memoryStream.Seek(0, SeekOrigin.Begin);
                    memoryStream.CopyTo(fileStream);
                }
            }
        }

        private static byte[] ConvertToByteArray(BitmapImage image)
        {
            var encoder = new JpegBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using (MemoryStream ms = new MemoryStream())
            {
                encoder.Save(ms);
                return ms.ToArray();
            }
        }
    }
}