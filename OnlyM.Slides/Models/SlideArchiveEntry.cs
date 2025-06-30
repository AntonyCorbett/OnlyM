using System.Windows.Media.Imaging;

namespace OnlyM.Slides.Models;

internal sealed class SlideArchiveEntry
{
    public string? ArchiveEntryName { get; init; }

    public BitmapSource? Image { get; init; }
}
