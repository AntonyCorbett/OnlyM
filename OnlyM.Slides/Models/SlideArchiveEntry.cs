﻿using System.Windows.Media.Imaging;

namespace OnlyM.Slides.Models;

internal sealed class SlideArchiveEntry
{
    public string? ArchiveEntryName { get; set; }

    public BitmapSource? Image { get; set; }
}
