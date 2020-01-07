namespace OnlyM.Services.PdfOptions
{
    using System.Collections.Generic;
    using OnlyM.Models;

    internal interface IPdfOptionsService
    {
        void Init(IEnumerable<MediaItem> items);

        void Add(string path, PdfOptions options);
    }
}
