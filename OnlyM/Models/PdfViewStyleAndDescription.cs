namespace OnlyM.Models;

public class PdfViewStyleAndDescription
{
    public PdfViewStyle Style { get; init; }

    public string? Description { get; init; }

    public override string ToString() => Description ?? string.Empty;
}
