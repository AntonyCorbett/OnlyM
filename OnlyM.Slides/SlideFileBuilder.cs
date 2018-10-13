namespace OnlyM.Slides
{
    using System;
    using System.IO;
    using System.IO.Compression;
    using Newtonsoft.Json;
    using OnlyM.Slides.Models;

    public class SlideFileBuilder
    {
        private const string ConfigEntryName = @"config.json";
        private readonly SlidesConfig _config = new SlidesConfig();
        
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

                    foreach (var image in _config.Slides)
                    {
                        if (!File.Exists(image.OriginalFilePath))
                        {
                            throw new Exception($"Could not find image file: {image.OriginalFilePath}");
                        }

                        zip.CreateEntryFromFile(image.OriginalFilePath, image.ArchiveEntryName);
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
    }
}