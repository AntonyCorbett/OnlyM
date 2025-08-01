﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using OnlyM.CoreSys;
using OnlyM.Slides.Models;

namespace OnlyM.Slides.Helpers;

internal sealed class SlideArchiveEntryBatchHelper
{
    private readonly IReadOnlyList<Slide> _slides;
    private readonly int _maxSlideWidth;
    private readonly int _maxSlideHeight;
    private readonly int _batchSize;

    private int _currentIndex;

    public SlideArchiveEntryBatchHelper(
        IReadOnlyList<Slide> slides, int maxSlideWidth, int maxSlideHeight, int batchSize)
    {
        _slides = slides;
        _maxSlideWidth = maxSlideWidth;
        _maxSlideHeight = maxSlideHeight;
        _batchSize = batchSize;
    }

#pragma warning disable U2U1011
    public IReadOnlyList<SlideArchiveEntry>? GetBatch()
#pragma warning restore U2U1011
    {
        var slideBatch = GetSlideBatch();
        if (slideBatch == null)
        {
            return null;
        }

        var result = new List<SlideArchiveEntry>();

        var map = new ConcurrentDictionary<Slide, BitmapSource>();

        Parallel.ForEach(slideBatch, slide =>
        {
            var image = GetImage(slide);
            if (image != null)
            {
                map.TryAdd(slide, image);
            }
        });

        foreach (var slide in slideBatch)
        {
            result.Add(new SlideArchiveEntry
            {
                ArchiveEntryName = slide.ArchiveEntryName,
                Image = map[slide],
            });
        }

        return result;
    }

    private BitmapSource? GetImage(Slide slide)
    {
        if (slide.Image != null)
        {
            return slide.Image;
        }

        if (string.IsNullOrEmpty(slide.OriginalFilePath))
        {
            return null;
        }

        if (!File.Exists(slide.OriginalFilePath))
        {
            throw new Exception($"Could not find image file: {slide.OriginalFilePath}");
        }

        return GraphicsUtils.GetImageAutoRotatedAndResized(
            slide.OriginalFilePath, _maxSlideWidth, _maxSlideHeight);
    }

    private List<Slide>? GetSlideBatch()
    {
        if (_currentIndex >= _slides.Count)
        {
            return null;
        }

        var result = new List<Slide>(_batchSize);

        var count = 0;
        while (_currentIndex < _slides.Count && count < _batchSize)
        {
            result.Add(_slides[_currentIndex]);

            ++_currentIndex;
            ++count;
        }

        return result;
    }
}
