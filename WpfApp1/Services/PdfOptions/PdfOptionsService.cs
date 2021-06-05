namespace OnlyM.Services.PdfOptions
{
    using System.Collections.Generic;
    using OnlyM.Models;
    
    internal class PdfOptionsService : IPdfOptionsService
    {
        private readonly Dictionary<string, PdfOptions> _items = new Dictionary<string, PdfOptions>();

        public void Init(IEnumerable<MediaItem> items)
        {
            foreach (var item in items)
            {
                if (item.IsPdf)
                {
                    if (_items.ContainsKey(item.FilePath))
                    {
                        item.ChosenPdfPage = _items[item.FilePath].PageNumber.ToString();
                        item.ChosenPdfViewStyle = _items[item.FilePath].Style;
                    }
                    else
                    {
                        var options = new PdfOptions { PageNumber = GetPageNumber(item.ChosenPdfPage), Style = item.ChosenPdfViewStyle };
                        Add(item.FilePath, options);
                    }
                }
            }
        }

        public void Add(string path, PdfOptions options)
        {
            if (_items.ContainsKey(path))
            {
                _items[path] = options;
            }
            else
            {
                _items.Add(path, options);
            }
        }

        private int GetPageNumber(string pageNumberString)
        {
            if (!int.TryParse(pageNumberString, out var pageNumber))
            {
                return 1;
            }

            return pageNumber;
        }
    }
}
