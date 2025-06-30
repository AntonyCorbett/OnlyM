namespace OnlyM.Core.Models;

public class SupportedMediaType
{
    public string? Name { get; init; }

    public MediaClassification Classification { get; init; }

    public string? FileExtension { get; init; }
}
