namespace OnlyM.Core.Models;

public class MediaFile
{
    public string? FullPath { get; init; }

    public SupportedMediaType? MediaType { get; init; }

    public long LastChanged { get; init; }
}
