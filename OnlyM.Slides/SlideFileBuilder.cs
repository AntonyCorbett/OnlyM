using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows.Media.Imaging;
using Newtonsoft.Json;
using OnlyM.Slides.Helpers;
using OnlyM.Slides.Models;

namespace OnlyM.Slides
{
    public class SlideFileBuilder
    {
        private const string ConfigEntryName = "config.json";
        private readonly SlidesConfig _config = new();
        private readonly int _maxSlideWidth;
        private readonly int _maxSlideHeight;

        public SlideFileBuilder(int maxSlideWidth, int maxSlideHeight)
        {
            _maxSlideWidth = maxSlideWidth;
            _maxSlideHeight = maxSlideHeight;
        }

        public event EventHandler<BuildProgressEventArgs> BuildProgressEvent;

        public bool AutoPlay
        {
            get => _config.AutoPlay;
            set => _config.AutoPlay = value;
        }

        public bool AutoClose
        {
            get => _config.AutoClose;
            set => _config.AutoClose = value;
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

        public int SlideCount => _config.SlideCount;

        public string CreateSignature() => _config.CreateSignature();
        
        public IReadOnlyCollection<Slide> GetSlides() => _config.Slides;
        
        public void RemoveSlide(string slideName) => _config.RemoveSlide(slideName);
        
        public void SyncSlideOrder(IEnumerable<string> slideNames) => _config.SyncSlideOrder(slideNames);
        
        public void Load(string slideshowPath)
        {
            if (string.IsNullOrEmpty(slideshowPath))
            {
                throw new ArgumentNullException(nameof(slideshowPath));
            }

            var f = new SlideFile(slideshowPath);
            AutoPlay = f.AutoPlay;
            AutoClose = f.AutoClose;
            DwellTimeMilliseconds = f.DwellTimeMilliseconds;
            Loop = f.Loop;

            foreach (var slide in f.GetSlides(true))
            {
                _config.Slides.Add(slide);
            }
        }

        public void InsertSlide(
            int index,
            string bitmapPath,
            bool fadeInForward,
            bool fadeInReverse,
            bool fadeOutForward,
            bool fadeOutReverse,
            int dwellTimeMilliseconds = 0)
        {
            if (index < 0 || index > _config.SlideCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var slide = CreateSlide(
                bitmapPath,
                fadeInForward,
                fadeInReverse,
                fadeOutForward,
                fadeOutReverse,
                dwellTimeMilliseconds);

            _config.Slides.Insert(index, slide);
        }

        public void AddSlide(
            string bitmapPath, 
            bool fadeInForward,
            bool fadeInReverse,
            bool fadeOutForward,
            bool fadeOutReverse,
            int dwellTimeMilliseconds = 0)
        {
            var slide = CreateSlide(
                bitmapPath,
                fadeInForward,
                fadeInReverse,
                fadeOutForward,
                fadeOutReverse,
                dwellTimeMilliseconds);

            _config.Slides.Add(slide);
        }

        public Slide GetSlide(string itemName)
        {
            return _config.Slides.SingleOrDefault(x => x.ArchiveEntryName.Equals(itemName, StringComparison.OrdinalIgnoreCase));
        }

        public void Build(string path, bool overwrite)
        {
            BuildProgress(0);

            var numEntriesToBuild = _config.SlideCount + 1;
            var numEntriesBuilt = 0;

            CreateEmptyArchive(path, overwrite);

            BuildProgress(CalcPercentComplete(++numEntriesBuilt, numEntriesToBuild));

            var batchSize = Environment.Is64BitProcess ? 8 : 4;
            var batchHelper = new SlideArchiveEntryBatchHelper(_config.Slides, _maxSlideWidth, _maxSlideHeight, batchSize);

            var batch = batchHelper.GetBatch();
            while (batch != null)
            {
                AddBitmapImagesToArchive(path, batch, numEntriesBuilt, numEntriesToBuild);
                numEntriesBuilt += batch.Count;
                batch = batchHelper.GetBatch();
            }
        }

        private static double CalcPercentComplete(int numEntriesBuilt, int numEntriesToBuild)
            => (double)numEntriesBuilt * 100 / numEntriesToBuild;

        private void CreateEmptyArchive(string path, bool overwrite)
        {
            if (File.Exists(path) && !overwrite)
            {
                throw new Exception("File already exists!");
            }

            File.Delete(path);
            if (File.Exists(path))
            {
                throw new Exception("File could not be deleted!");
            }

            _config.Sanitize();

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
                }

                using (var fileStream = new FileStream(path, FileMode.Create))
                {
                    memoryStream.Seek(0, SeekOrigin.Begin);
                    memoryStream.CopyTo(fileStream);
                }
            }
        }

        private void AddBitmapImagesToArchive(
            string zipArchivePath, IReadOnlyList<SlideArchiveEntry> slides, int numEntriesBuilt, int numEntriesToBuild)
        {
            using (var zip = ZipFile.Open(zipArchivePath, ZipArchiveMode.Update))
            {
                foreach (var slide in slides)
                {
                    AddBitmapImageToArchive(zip, slide.ArchiveEntryName, slide.Image);
                    BuildProgress(CalcPercentComplete(++numEntriesBuilt, numEntriesToBuild));
                }
            }
        }

        private static void AddBitmapImageToArchive(ZipArchive zip, string slideArchiveEntryName, BitmapSource slideImage)
        {
            // This is a little odd (multiple streams and rewinding the memory stream!).
            // I can't find a better way of saving a BitmapSource in the archive entry.
            BitmapEncoder encoder = new JpegBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(slideImage));

            using (var ms = new MemoryStream())
            {
                encoder.Save(ms);
                ms.Seek(0, SeekOrigin.Begin);

                var entry = zip.CreateEntry(slideArchiveEntryName, CompressionLevel.Optimal);
                using (var entryStream = entry.Open())
                {
                    ms.CopyTo(entryStream);
                }
            }
        }

        private Slide CreateSlide(
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

            var archiveEntryName = GenerateUniqueArchiveEntryName(bitmapPath);

            if (string.IsNullOrEmpty(archiveEntryName))
            {
                throw new ArgumentException("Could not extract archive entry name", nameof(bitmapPath));
            }

            var result = new Slide
            {
                ArchiveEntryName = archiveEntryName,
                OriginalFilePath = bitmapPath,
                FadeInForward = fadeInForward,
                FadeInReverse = fadeInReverse,
                FadeOutForward = fadeOutForward,
                FadeOutReverse = fadeOutReverse,
                DwellTimeMilliseconds = dwellTimeMilliseconds,
            };

            return result;
        }

        private string GenerateUniqueArchiveEntryName(string bitmapPath)
        {
            const int maxAttempts = 100;

            var baseName = Path.GetFileNameWithoutExtension(bitmapPath);

            var similarSlideNames = GetSlideNamesStartingWith(baseName).ToArray();
            if (similarSlideNames.Length > 0)
            {
                for (var n = 2; n < maxAttempts; ++n)
                {
                    var candidate = $"{baseName} {n:D3}";
                    if (!similarSlideNames.Contains(candidate, StringComparer.OrdinalIgnoreCase))
                    {
                        return candidate;
                    }
                }

                return null;
            }

            return baseName;
        }

        private IEnumerable<string> GetSlideNamesStartingWith(string s)
        {
            return _config.Slides.Where(x => x.ArchiveEntryName.StartsWith(s, StringComparison.OrdinalIgnoreCase)).Select(x => x.ArchiveEntryName);
        }

        private void BuildProgress(double percentageComplete, string entryName = null)
        {
            BuildProgressEvent?.Invoke(this, new BuildProgressEventArgs
            {
                EntryName = entryName,
                PercentageComplete = percentageComplete,
            });
        }
    }
}
