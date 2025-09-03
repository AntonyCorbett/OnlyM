using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using OnlyM.Core.Services.Media;
using OnlyM.Core.Services.Options;
using OnlyM.Core.Services.WebShortcuts;
using OnlyM.Core.Utils;
using OnlyM.CoreSys.Services.Snackbar;
using OnlyM.EventTracking;
using OnlyM.Models;
using OnlyM.Slides;
using Serilog;
using Serilog.Events;

namespace OnlyM.Services.DragAndDrop;

// ReSharper disable once ClassNeverInstantiated.Global
internal sealed class DragAndDropService : IDragAndDropService
{
    private const string ClipboardImageDataFileNameStart = "OnlyM_Clipboard_";

    private readonly IMediaProviderService _mediaProviderService;
    private readonly IOptionsService _optionsService;
    private readonly ISnackbarService _snackbarService;
    private readonly object _imageDataPasteLock = new();
    private bool _canDrop;
    private int _currentClipboardImageIndex = -1; // -1 = uninitialized

    public DragAndDropService(
        IMediaProviderService mediaProviderService,
        IOptionsService optionsService,
        ISnackbarService snackbarService)
    {
        _mediaProviderService = mediaProviderService;
        _optionsService = optionsService;
        _snackbarService = snackbarService;
    }

    public event EventHandler<FilesCopyProgressEventArgs>? CopyingFilesProgressEvent;

    public void Init(FrameworkElement targetElement)
    {
        targetElement.DragEnter += HandleDragEnter;
        targetElement.DragOver += HandleDragOver;
        targetElement.Drop += HandleDrop;
    }

    public void Paste()
    {
        var data = Clipboard.GetDataObject();
        if (data != null)
        {
            DoCopy(data);
        }
    }

    private void HandleDragOver(object? sender, DragEventArgs e)
    {
        SetEffects(e);
        e.Handled = true;
    }

    private void DoCopy(IDataObject data)
    {
        if (CanDropOrPasteFiles(data))
        {
            CopyMediaFiles(data);
        }
        else if (CanPasteImageData(data))
        {
            CopyImageData(data);
        }
        else if (CanDropOrPasteUris(data))
        {
            CopyUris(data);
        }
    }

    private void HandleDrop(object? sender, DragEventArgs e)
    {
        if (e.Data != null)
        {
            DoCopy(e.Data);
        }
    }

    private void HandleDragEnter(object? sender, DragEventArgs e)
    {
        // do we allow drop of drag object?
        _canDrop = CanDropOrPasteFiles(e.Data) || CanPasteImageData(e.Data) || CanDropOrPasteUris(e.Data);
        SetEffects(e);
        e.Handled = true;
    }

    private void SetEffects(DragEventArgs e) =>
        e.Effects = _canDrop ? DragDropEffects.Copy : DragDropEffects.None;

    private void CopyMediaFiles(IDataObject data) =>
        Task.Run(() =>
        {
            var count = InternalCopyMediaFiles(data, out var someError);
            DisplaySnackbar(count, someError);
        });

    private void CopyImageData(IDataObject data) =>
        Task.Run(() =>
        {
            var count = InternalCopyImageData(data, out var someError);
            DisplaySnackbar(count, someError);
        });

    private void CopyUris(IDataObject data) =>
        Task.Run(() =>
        {
            var count = InternalCopyUris(data, out var someError);
            DisplaySnackbar(count, someError);
        });

    private void DisplaySnackbar(int count, bool someError)
    {
        if (someError)
        {
            _snackbarService.EnqueueWithOk(Properties.Resources.COPYING_ERROR, Properties.Resources.OK);
        }
        else if (count == 0)
        {
            _snackbarService.EnqueueWithOk(Properties.Resources.NO_SUPPORTED_FILES, Properties.Resources.OK);
        }
        else if (count == 1)
        {
            _snackbarService.EnqueueWithOk(Properties.Resources.FILE_COPIED, Properties.Resources.OK);
        }
        else
        {
#pragma warning disable CA1863
            _snackbarService.EnqueueWithOk(string.Format(CultureInfo.CurrentCulture, Properties.Resources.FILES_COPIED, count), Properties.Resources.OK);
#pragma warning restore CA1863
        }
    }

    private int InternalCopyMediaFiles(IDataObject data, out bool someError)
    {
        var count = 0;
        someError = false;

        OnCopyingFilesProgressEvent(new FilesCopyProgressEventArgs { Status = FileCopyStatus.StartingCopy });
        try
        {
            var mediaFolder = _optionsService.MediaFolder;

            var files = GetSupportedFiles(data).ToArray();
            if (files.Length == 0)
            {
                return 0;
            }

            var shouldCreateSlideshow = DataIsFromOnlyV(data) && files.Length > 1;

            count = shouldCreateSlideshow
                ? CopyAsSlideshow(mediaFolder, data, files)
                : CopyAsIndividualFiles(mediaFolder, files);
        }
        catch (Exception ex)
        {
            EventTracker.Error(ex, "Copying media files");
            Log.Logger.Error(ex, "Could not copy media files");
            someError = true;
        }
        finally
        {
            OnCopyingFilesProgressEvent(new FilesCopyProgressEventArgs { Status = FileCopyStatus.FinishedCopy });
        }

        return count;
    }

    private int InternalCopyImageData(IDataObject data, out bool someError)
    {
        var count = 0;
        someError = false;

        OnCopyingFilesProgressEvent(new FilesCopyProgressEventArgs { Status = FileCopyStatus.StartingCopy });
        try
        {
            var mediaFolder = _optionsService.MediaFolder;

            var bmpObj = data.GetData(DataFormats.Bitmap);
            if (bmpObj is System.Windows.Media.Imaging.BitmapSource bmpSource)
            {
                using var bitmap = BitmapSourceToBitmap(bmpSource);
                count = CopyFromImageData(mediaFolder, bitmap);
            }
            else if (data.GetDataPresent(DataFormats.Dib))
            {
                var dibObj = data.GetData(DataFormats.Dib);
                if (dibObj is MemoryStream dibStream)
                {
                    using var bmp = DibToBitmap(dibStream);
                    if (bmp != null)
                    {
                        count = CopyFromImageData(mediaFolder, bmp);
                    }
                }
                else if (dibObj is byte[] dibBytes)
                {
                    using var ms = new MemoryStream(dibBytes);
                    using var bmp = DibToBitmap(ms);
                    if (bmp != null)
                    {
                        count = CopyFromImageData(mediaFolder, bmp);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            EventTracker.Error(ex, "Copying image data");
            Log.Logger.Error(ex, "Could not copy image data");
            someError = true;
        }
        finally
        {
            OnCopyingFilesProgressEvent(new FilesCopyProgressEventArgs { Status = FileCopyStatus.FinishedCopy });
        }

        return count;
    }

    // Converts WPF BitmapSource to System.Drawing.Bitmap
    private static Bitmap BitmapSourceToBitmap(System.Windows.Media.Imaging.BitmapSource source)
    {
        using var ms = new MemoryStream();
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(source));
        encoder.Save(ms);
        ms.Position = 0;
        return new Bitmap(ms);
    }

    // Converts a DIB stream to a Bitmap
    private static Bitmap? DibToBitmap(MemoryStream dibStream)
    {
        // DIB does not include a BITMAPFILEHEADER, so we need to add it
        // See: https://stackoverflow.com/a/1468847/1768303
        const int bitmapFileHeaderSize = 14;

        dibStream.Position = 0;
        var dibLength = (int)dibStream.Length;
        var dibBytes = new byte[dibLength];
        dibStream.ReadExactly(dibBytes, 0, dibLength);

        // Get the size of the DIB header
        var headerSize = BitConverter.ToInt32(dibBytes, 0);

        // Calculate the file size
        var fileSize = bitmapFileHeaderSize + dibLength;

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // BITMAPFILEHEADER
        bw.Write((ushort)0x4D42); // 'BM'
        bw.Write(fileSize);
        bw.Write((ushort)0); // reserved1
        bw.Write((ushort)0); // reserved2
        bw.Write(bitmapFileHeaderSize + headerSize); // offset to pixel data

        // DIB data
        bw.Write(dibBytes);

        ms.Position = 0;
        try
        {
            return new Bitmap(ms);
        }
        catch
        {
            return null;
        }
    }

    private int InternalCopyUris(IDataObject data, out bool someError)
    {
        var count = 0;
        someError = false;

        OnCopyingFilesProgressEvent(new FilesCopyProgressEventArgs { Status = FileCopyStatus.StartingCopy });
        try
        {
            var mediaFolder = _optionsService.MediaFolder;

            var uriList = GetSupportedUrls(data).ToArray();
            if (uriList.Length == 0)
            {
                return 0;
            }

            count = CopyAsIndividualUris(mediaFolder, uriList);
        }
        catch (Exception ex)
        {
            EventTracker.Error(ex, "Copying URIs");
            Log.Logger.Error(ex, "Could not copy Uris");
            someError = true;
        }
        finally
        {
            OnCopyingFilesProgressEvent(new FilesCopyProgressEventArgs { Status = FileCopyStatus.FinishedCopy });
        }

        return count;
    }

    private static int CopyAsSlideshow(string mediaFolder, IDataObject data, string[] files)
    {
        var title = GetOnlyVTitle(data);
        if (string.IsNullOrEmpty(title))
        {
            return 0;
        }

        const int maxSlideWidth = 1920;
        const int maxSlideHeight = 1080;

        var sfb = new SlideFileBuilder(maxSlideWidth, maxSlideHeight) { AutoPlay = false, Loop = false };

        for (var n = 0; n < files.Length; ++n)
        {
            var file = files[n];
            sfb.AddSlide(file, n == 0, false, n == file.Length - 1, false);
        }

        var destFilename = Path.Combine(mediaFolder, title + SlideFile.FileExtension);
        sfb.Build(destFilename, overwrite: true);

        return 1;
    }

    private static int CopyAsIndividualFiles(string mediaFolder, string[] files)
    {
        var count = 0;

        foreach (var file in files)
        {
            var filename = Path.GetFileName(file);

            if (!string.IsNullOrEmpty(filename))
            {
                var destFile = Path.Combine(mediaFolder, filename);
                if (CopyFileInternal(file, destFile))
                {
                    ++count;
                }
            }
        }

        return count;
    }

    private int CopyFromImageData(string mediaFolder, Bitmap image)
    {
        lock (_imageDataPasteLock)
        {
            if (_currentClipboardImageIndex == -1)
            {
                InitialiseCurrentClipboardImageIndex();
            }
            
            // this is better for ths folder watcher which triggers as soon as a file write 
            // begins. A large file would not be completely written before the folder watcher
            // triggers an attempt to analyse the file, extract thumbnail etc.
            var tempFileName = Path.Combine(mediaFolder, Path.GetRandomFileName());
            image.Save(tempFileName, System.Drawing.Imaging.ImageFormat.Png);
            var destFilePath = Path.Combine(mediaFolder, $"{ClipboardImageDataFileNameStart}{++_currentClipboardImageIndex:D3}.png");
            File.Move(tempFileName, destFilePath);
            return 1;
        }
    }

    private void InitialiseCurrentClipboardImageIndex()
    {
        _currentClipboardImageIndex = 1;

        var files = _mediaProviderService.GetMediaFiles()
            .Select(x => Path.GetFileNameWithoutExtension(x.FullPath))
            .Where(x => x?.StartsWith(ClipboardImageDataFileNameStart, StringComparison.OrdinalIgnoreCase) == true);

        foreach (var file in files)
        {
            var suffix = file![ClipboardImageDataFileNameStart.Length..];
            if (int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
            {
                if (index > _currentClipboardImageIndex)
                {
                    _currentClipboardImageIndex = index;
                }
            }
        }
    }

    private int CopyAsIndividualUris(string mediaFolder, string[] uriList)
    {
        var count = 0;

        foreach (var uri in uriList)
        {
            if (string.IsNullOrEmpty(uri))
            {
                continue;
            }

            if (IsMediaFileUrl(uri))
            {
                // uri points to a media item.
                if (CopyAsMediaFileFromUri(mediaFolder, uri))
                {
                    ++count;
                }
            }
            else
            {
                // uri should be treated as a web shortcut.
                if (CreateShortcutFromUri(mediaFolder, uri))
                {
                    ++count;
                }
            }
        }

        return count;
    }

    private static bool CreateShortcutFromUri(string mediaFolder, string uri)
    {
        var url = new Uri(uri);

        var filename = GenerateLocalFileName(url);
        var destFile = Path.Combine(mediaFolder, filename);

        if (File.Exists(destFile))
        {
            return false;
        }

        var sourceFile = Path.Combine(GetWebDownloadTempFolder(), filename);

        if (string.IsNullOrEmpty(sourceFile))
        {
            return false;
        }

        WebShortcutHelper.Generate(sourceFile, url);

        if (!File.Exists(sourceFile))
        {
            return false;
        }

        if (!CopyFileInternal(sourceFile, destFile))
        {
            return false;
        }

        FileUtils.SafeDeleteFile(sourceFile);

        return true;
    }

    private static bool CopyAsMediaFileFromUri(string mediaFolder, string uri)
    {
        var filename = Path.GetFileName(uri);
        if (string.IsNullOrEmpty(filename))
        {
            return false;
        }

        var destFile = Path.Combine(mediaFolder, filename);

        if (File.Exists(destFile))
        {
            return false;
        }

        var sourceFile = Path.Combine(GetWebDownloadTempFolder(), filename);

        if (string.IsNullOrEmpty(sourceFile))
        {
            return false;
        }

        // download a local copy of the media.
        if (!FileDownloader.Download(new Uri(uri), sourceFile, true))
        {
            return false;
        }

        if (!File.Exists(sourceFile))
        {
            return false;
        }

        if (!CopyFileInternal(sourceFile, destFile))
        {
            return false;
        }

        FileUtils.SafeDeleteFile(sourceFile);

        return true;
    }

    private static string GenerateLocalFileName(Uri uri)
    {
        var hashCode = uri.GetHashCode();

        var webPageTitle = WebPageTitleHelper.Get(uri);

        return !string.IsNullOrEmpty(webPageTitle)
            ? $"{FileUtils.CoerceValidFileName(webPageTitle)}{hashCode}.url"
            : $"{FileUtils.CoerceValidFileName(uri.Host)}-{hashCode}.url";
    }

    private static string GetWebDownloadTempFolder()
    {
        var result = Path.Combine(FileUtils.GetUsersTempFolder(), "OnlyM", "TempWebDownloads");
        FileUtils.CreateDirectory(result);
        return result;
    }

    private static bool CopyFileInternal(string sourceFile, string destFile)
    {
        if (string.IsNullOrEmpty(destFile) || File.Exists(destFile))
        {
            return false;
        }

        var destFolder = Path.GetDirectoryName(destFile);
        if (string.IsNullOrEmpty(destFolder))
        {
            return false;
        }

        // this is better for ths folder watcher which triggers as soon as a file write 
        // begins. A large file would not be completely written before the folder watcher
        // triggers an attempt to analyse the file, extract thumbnail etc.
        var tempFileName = Path.Combine(destFolder, Path.GetRandomFileName());
        File.Copy(sourceFile, tempFileName, true);
        File.Move(tempFileName, destFile);

        return true;
    }

    private bool IsMediaFileUrl(string uri)
    {
        var ext = Path.GetExtension(uri);
        return !string.IsNullOrEmpty(ext) && _mediaProviderService.IsFileExtensionSupported(ext);
    }

    private bool CanDropOrPasteFiles(IDataObject data) => GetSupportedFiles(data).Count > 0;

    private static bool CanPasteImageData(IDataObject data) => IsSupportedImageData(data);

    private static bool CanDropOrPasteUris(IDataObject data) => GetSupportedUrls(data).Count > 0;

    private static bool DataIsFromOnlyV(IDataObject data)
    {
        if (!data.GetDataPresent(DataFormats.StringFormat))
        {
            return false;
        }

        var s = (string?)data.GetData(DataFormats.StringFormat);
        return s != null && s.StartsWith("OnlyV|", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetOnlyVTitle(IDataObject data)
    {
        var s = (string?)data.GetData(DataFormats.StringFormat);
        return s?.Split('|')[1];
    }

    private static List<string> GetSupportedUrls(IDataObject data)
    {
        if (!data.GetDataPresent(DataFormats.StringFormat))
        {
            return [];
        }

        var s = (string?)data.GetData(DataFormats.StringFormat);

        if (string.IsNullOrEmpty(s))
        {
            return [];
        }

        var result = new List<string>();

        using var reader = new StringReader(s);
        var line = reader.ReadLine();
        if (!string.IsNullOrEmpty(line) && IsAcceptableUri(line))
        {
            result.Add(line.Trim());
        }

        return result;
    }

    private static bool IsAcceptableUri(string uri) =>
        Uri.TryCreate(uri, UriKind.Absolute, out var uriResult) &&
        (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);

    private List<string> GetSupportedFiles(IDataObject data)
    {
        var result = new List<string>();

        if (data.GetDataPresent(DataFormats.FileDrop))
        {
            // Note that you can have more than one file...
            var files = (string[]?)data.GetData(DataFormats.FileDrop);

            if (files == null || files.Length == 0)
            {
                return result;
            }

            foreach (var file in files)
            {
                if (Directory.Exists(file))
                {
                    // a folder rather than a file.
                    foreach (var fileInFolder in Directory.GetFiles(file))
                    {
                        var fileToAdd = GetSupportedFile(fileInFolder);
                        if (fileToAdd != null)
                        {
                            result.Add(fileToAdd);
                        }
                    }
                }
                else
                {
                    var fileToAdd = GetSupportedFile(file);
                    if (fileToAdd != null)
                    {
                        result.Add(fileToAdd);
                    }
                }
            }
        }

        if (Log.Logger.IsEnabled(LogEventLevel.Verbose))
        {
            Log.Logger.Verbose("Found {Count} supported files in drag-and-drop operation", result.Count);
        }

        result.Sort();

        return result;
    }

    private static bool IsSupportedImageData(IDataObject data)
    {
        // Try Bitmap first
        if (data.GetDataPresent(DataFormats.Bitmap))
        {
            var d = data.GetData(DataFormats.Bitmap);
            if (d is not null)
            {
                return true;
            }
        }

        // Try DIB (Device Independent Bitmap) as fallback
        if (data.GetDataPresent(DataFormats.Dib))
        {
            var dib = data.GetData(DataFormats.Dib);
            if (dib is not null)
            {
                return true;
            }
        }

        return false;
    }

    private string? GetSupportedFile(string file)
    {
        var ext = Path.GetExtension(file);
        if (string.IsNullOrEmpty(ext) || !_mediaProviderService.IsFileExtensionSupported(ext))
        {
            return null;
        }

        return file;
    }

    private void OnCopyingFilesProgressEvent(FilesCopyProgressEventArgs e) =>
        CopyingFilesProgressEvent?.Invoke(this, e);
}
