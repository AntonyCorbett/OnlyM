﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Windows.Media.Imaging;
using Newtonsoft.Json;
using OnlyM.Slides.Models;

namespace OnlyM.Slides;

public class SlideFile
{
    private const string ConfigEntryName = "config.json";
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
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var slide = _config.Slides[index];
        if (slide.ArchiveEntryName == null && includeBitmapImage)
        {
            throw new NotSupportedException("Missing archive entry name");
        }

        using var zip = ZipFile.OpenRead(FilePath);
        var image = includeBitmapImage
            ? ReadBackgroundImage(zip, slide.ArchiveEntryName!)
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

    public IReadOnlyCollection<Slide> GetSlides(bool includeBitmapImage)
    {
        var result = new List<Slide>(_config.SlideCount);

        using (var zip = ZipFile.OpenRead(FilePath))
        {
            foreach (var slide in _config.Slides)
            {
                if (slide.ArchiveEntryName == null && includeBitmapImage)
                {
                    throw new NotSupportedException("Missing archive entry name");
                }

                var image = includeBitmapImage
                    ? ReadBackgroundImage(zip, slide.ArchiveEntryName!)
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

        using var zip = ZipFile.OpenRead(FilePath);
        foreach (var slide in _config.Slides)
        {
            if (slide.ArchiveEntryName != null)
            {
                var entry = zip.GetEntry(slide.ArchiveEntryName);
                entry?.ExtractToFile(Path.Combine(folder, slide.ArchiveEntryName), overwrite: true);
            }
        }
    }

    private SlidesConfig Load()
    {
        using var zip = ZipFile.OpenRead(FilePath);
        var configEntry = zip.GetEntry(ConfigEntryName) ?? throw new Exception($"Could not find {ConfigEntryName} entry");

        using var stream = configEntry.Open();
        var serializer = new JsonSerializer();

        using var sr = new StreamReader(stream);
        using var jsonTextReader = new JsonTextReader(sr);
        return serializer.Deserialize<SlidesConfig>(jsonTextReader)
               ?? throw new Exception($"Could not read {ConfigEntryName} entry");
    }

    private static BitmapImage ReadBackgroundImage(ZipArchive zip, string entryName)
    {
        var entry = zip.GetEntry(entryName) ?? throw new Exception($"Could not read {entryName} entry");

        using var stream = entry.Open();
        using var memoryStream = new MemoryStream();

        stream.CopyTo(memoryStream);

        BitmapImage bmp;
        try
        {
            bmp = CreateBitmapImage(memoryStream, ignoreColorProfile: false);
        }
        catch (ArgumentException)
        {
            // probably colour profile corruption
            bmp = CreateBitmapImage(memoryStream, ignoreColorProfile: true);
        }

        return bmp;
    }

    private static BitmapImage CreateBitmapImage(MemoryStream stream, bool ignoreColorProfile)
    {
        stream.Position = 0;

        var bitmap = new BitmapImage();

        bitmap.BeginInit();
        bitmap.CreateOptions = ignoreColorProfile ? BitmapCreateOptions.IgnoreColorProfile : BitmapCreateOptions.None;
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();

        return bitmap;
    }
}
