namespace OnlyM.Slides
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Windows.Media.Imaging;
    using Newtonsoft.Json;
    using OnlyM.Slides.Models;

    public class SlideFile
    {
        private const string ConfigEntryName = @"config.json";
        private readonly SlidesConfig _config;
        
        public SlideFile(string path)
        {
            FilePath = path;
            _config = Load();
        }

        public static string FileExtension => ".omslide";

        public string FilePath { get; }

        public bool AutoPlay => _config.AutoPlay;

        public bool AutoClose => _config.AutoClose;

        public int DwellTimeMilliseconds => _config.DwellTimeMilliseconds;

        public bool Loop => _config.Loop;

        public int SlideCount => _config.SlideCount;

        public Slide GetSlide(int index, bool includeBitmapImage = true)
        {
            if (index < 0 || index > SlideCount - 1)
            {
                throw new IndexOutOfRangeException();
            }

            var slide = _config.Slides[index];

            using (var zip = ZipFile.OpenRead(FilePath))
            {
                var image = includeBitmapImage
                    ? ReadBackgroundImage(zip, slide.ArchiveEntryName)
                    : null;

                return new Slide
                {
                    OriginalFilePath = slide.OriginalFilePath,
                    ArchiveEntryName = slide.ArchiveEntryName,
                    FadeInForward = slide.FadeInForward,
                    FadeInReverse = slide.FadeInReverse,
                    FadeOutForward = slide.FadeOutForward,
                    FadeOutReverse = slide.FadeOutReverse,
                    DwellTimeMilliseconds = slide.DwellTimeMilliseconds,
                    Image = image,
                };
            }
        }

        public IReadOnlyCollection<Slide> GetSlides(bool includeBitmapImage)
        {
            var result = new List<Slide>();

            using (var zip = ZipFile.OpenRead(FilePath))
            {
                foreach (var slide in _config.Slides)
                {
                    var image = includeBitmapImage
                        ? ReadBackgroundImage(zip, slide.ArchiveEntryName)
                        : null;

                    result.Add(new Slide
                    {
                        OriginalFilePath = slide.OriginalFilePath,
                        ArchiveEntryName = slide.ArchiveEntryName,
                        FadeInForward = slide.FadeInForward,
                        FadeInReverse = slide.FadeInReverse,
                        FadeOutForward = slide.FadeOutForward,
                        FadeOutReverse = slide.FadeOutReverse,
                        DwellTimeMilliseconds = slide.DwellTimeMilliseconds,
                        Image = image,
                    });
                }
            }

            return result;
        }

        public void ExtractImages(string folder)
        {
            Directory.CreateDirectory(folder);

            using (var zip = ZipFile.OpenRead(FilePath))
            {
                foreach (var slide in _config.Slides)
                {
                    var entry = zip.GetEntry(slide.ArchiveEntryName);
                    entry.ExtractToFile(Path.Combine(folder, slide.ArchiveEntryName), overwrite: true);
                }
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times", Justification = "trust streams behave properly")]
        private SlidesConfig Load()
        {
            using (var zip = ZipFile.OpenRead(FilePath))
            {
                var configEntry = zip.GetEntry(ConfigEntryName);
                if (configEntry == null)
                {
                    throw new Exception($"Could not find {ConfigEntryName} entry");
                }

                using (var stream = configEntry.Open())
                {
                    var serializer = new JsonSerializer();

                    using (var sr = new StreamReader(stream))
                    using (var jsonTextReader = new JsonTextReader(sr))
                    {
                        var config = serializer.Deserialize<SlidesConfig>(jsonTextReader);
                        if (config == null)
                        {
                            throw new Exception($"Could not read {ConfigEntryName} entry");
                        }

                        return config;
                    }
                }
            }
        }
        
        private BitmapImage ReadBackgroundImage(ZipArchive zip, string entryName)
        {
            var entry = zip.GetEntry(entryName);
            if (entry == null)
            {
                throw new Exception($"Could not read {entryName} entry");
            }

            using (var stream = entry.Open())
            using (var memoryStream = new MemoryStream())
            { 
                stream.CopyTo(memoryStream);
                memoryStream.Position = 0;

                var bitmap = new BitmapImage();

                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = memoryStream;
                bitmap.EndInit();

                return bitmap;
            }
        }
    }
}
