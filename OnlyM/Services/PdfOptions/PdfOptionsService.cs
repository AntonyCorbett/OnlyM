using System.Collections.Generic;
using System.Globalization;
using OnlyM.Models;

namespace OnlyM.Services.PdfOptions;

internal sealed class PdfOptionsService : IPdfOptionsService
{
    private readonly Dictionary<string, Models.PdfOptions> _items = new();

    public void Init(IEnumerable<MediaItem> items)
    {
        foreach (var item in items)
        {
            if (item.IsPdf && item.FilePath != null)
            {
                if (_items.TryGetValue(item.FilePath, out var opts))
                {
                    item.ChosenPdfPage = opts.PageNumber.ToString(CultureInfo.InvariantCulture);
                    item.ChosenPdfViewStyle = opts.Style;
                }
                else
                {
                    var options = new Models.PdfOptions { PageNumber = GetPageNumber(item.ChosenPdfPage), Style = item.ChosenPdfViewStyle };
                    Add(item.FilePath, options);
                }
            }
        }
    }

    public void Add(string path, Models.PdfOptions options) => _items[path] = options;

    private static int GetPageNumber(string pageNumberString) =>
        !int.TryParse(pageNumberString, out var pageNumber) ? 1 : pageNumber;
}
