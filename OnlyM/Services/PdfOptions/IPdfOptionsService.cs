using System.Collections.Generic;
using OnlyM.Models;

namespace OnlyM.Services.PdfOptions;

internal interface IPdfOptionsService
{
    void Init(IEnumerable<MediaItem> items);

    void Add(string path, Models.PdfOptions options);
}