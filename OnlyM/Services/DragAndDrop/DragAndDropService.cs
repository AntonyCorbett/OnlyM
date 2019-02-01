namespace OnlyM.Services.DragAndDrop
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows;
    using Core.Services.Media;
    using Core.Services.Options;
    using Models;
    using OnlyM.Core.Services.WebShortcuts;
    using OnlyM.Core.Utils;
    using OnlyM.CoreSys.Services.Snackbar;
    using OnlyM.Slides;
    using Serilog;

    // ReSharper disable once ClassNeverInstantiated.Global
    internal sealed class DragAndDropService : IDragAndDropService
    {
        private readonly IMediaProviderService _mediaProviderService;
        private readonly IOptionsService _optionsService;
        private readonly ISnackbarService _snackbarService;
        private bool _canDrop;

        public DragAndDropService(
            IMediaProviderService mediaProviderService,
            IOptionsService optionsService,
            ISnackbarService snackbarService)
        {
            _mediaProviderService = mediaProviderService;
            _optionsService = optionsService;
            _snackbarService = snackbarService;
        }

        public event EventHandler<FilesCopyProgressEventArgs> CopyingFilesProgressEvent;

        public void Init(FrameworkElement targetElement)
        {
            targetElement.DragEnter += HandleDragEnter;
            targetElement.DragOver += HandleDragOver;
            targetElement.Drop += HandleDrop;
        }

        public void Paste()
        {
            var data = Clipboard.GetDataObject();
            DoCopy(data);
        }

        private void HandleDragOver(object sender, DragEventArgs e)
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
            else if (CanDropOrPasteUris(data))
            {
                CopyUris(data);
            }
        }

        private void HandleDrop(object sender, DragEventArgs e)
        {
            DoCopy(e.Data);
        }

        private void HandleDragEnter(object sender, DragEventArgs e)
        {
            // do we allow drop of drag object?
            _canDrop = CanDropOrPasteFiles(e.Data) || CanDropOrPasteUris(e.Data);
            SetEffects(e);
            e.Handled = true;
        }

        private void SetEffects(DragEventArgs e)
        {
            e.Effects = _canDrop ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void CopyMediaFiles(IDataObject data)
        {
            if (data != null)
            {
                Task.Run(() =>
                {
                    int count = InternalCopyMediaFiles(data, out var someError);
                    DisplaySnackbar(count, someError);
                });
            }
        }

        private void CopyUris(IDataObject data)
        {
            if (data != null)
            {
                Task.Run(() =>
                {
                    int count = InternalCopyUris(data, out var someError);
                    DisplaySnackbar(count, someError);
                });
            }
        }

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
                _snackbarService.EnqueueWithOk(string.Format(Properties.Resources.FILES_COPIED, count), Properties.Resources.OK);
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
                if (!files.Any())
                {
                    return 0;
                }

                bool shouldCreateSlideshow = DataIsFromOnlyV(data) && files.Length > 1;

                count = shouldCreateSlideshow 
                    ? CopyAsSlideshow(mediaFolder, data, files) 
                    : CopyAsIndividualFiles(mediaFolder, files);
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Could not copy media files");
                someError = true;
            }
            finally
            {
                OnCopyingFilesProgressEvent(new FilesCopyProgressEventArgs { Status = FileCopyStatus.FinishedCopy });
            }

            return count;
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
                if (!uriList.Any())
                {
                    return 0;
                }

                count = CopyAsIndividualUris(mediaFolder, uriList);
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Could not copy Uris");
                someError = true;
            }
            finally
            {
                OnCopyingFilesProgressEvent(new FilesCopyProgressEventArgs { Status = FileCopyStatus.FinishedCopy });
            }

            return count;
        }

        private int CopyAsSlideshow(string mediaFolder, IDataObject data, string[] files)
        {
            var title = GetOnlyVTitle(data);
            if (!string.IsNullOrEmpty(title))
            {
                const int maxSlideWidth = 1920;
                const int maxSlideHeight = 1080;

                var sfb = new SlideFileBuilder(maxSlideWidth, maxSlideHeight) { AutoPlay = false, Loop = false };

                for (int n = 0; n < files.Length; ++n)
                {
                    var file = files[n];
                    sfb.AddSlide(file, n == 0, false, n == file.Length - 1, false);
                }

                var destFilename = Path.Combine(mediaFolder, title + SlideFile.FileExtension);
                sfb.Build(destFilename, overwrite: true);
                
                return 1;
            }

            return 0;
        }

        private int CopyAsIndividualFiles(string mediaFolder, string[] files)
        {
            int count = 0;

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

        private int CopyAsIndividualUris(string mediaFolder, string[] uriList)
        {
            int count = 0;

            foreach (var uri in uriList)
            {
                if (!string.IsNullOrEmpty(uri))
                {
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
            }

            return count;
        }

        private bool CreateShortcutFromUri(string mediaFolder, string uri)
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

        private bool CopyAsMediaFileFromUri(string mediaFolder, string uri)
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
            var downloader = new FileDownloader();
            if (!downloader.Download(new Uri(uri), sourceFile, true))
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

        private string GenerateLocalFileName(Uri uri)
        {
            var hashCode = uri.GetHashCode();

            var webPageTitle = WebPageTitleHelper.Get(uri);
            if (!string.IsNullOrEmpty(webPageTitle))
            {
                return $"{FileUtils.CoerceValidFileName(webPageTitle)}{hashCode}.url";
            }
            
            return $"{FileUtils.CoerceValidFileName(uri.Host)}-{hashCode}.url";
        }

        private string GetWebDownloadTempFolder()
        {
            var result = Path.Combine(FileUtils.GetUsersTempFolder(), "OnlyM", "TempWebDownloads");
            FileUtils.CreateDirectory(result);
            return result;
        }

        private bool CopyFileInternal(string sourceFile, string destFile)
        {
            if (!string.IsNullOrEmpty(destFile) && !File.Exists(destFile))
            {
                File.Copy(sourceFile, destFile, false);
                return true;
            }

            return false;
        }

        private bool IsMediaFileUrl(string uri)
        {
            var ext = Path.GetExtension(uri);
            return !string.IsNullOrEmpty(ext) && _mediaProviderService.IsFileExtensionSupported(ext);
        }

        private bool CanDropOrPasteFiles(IDataObject data)
        {
            return GetSupportedFiles(data).Any();
        }

        private bool CanDropOrPasteUris(IDataObject data)
        {
            return GetSupportedUrls(data).Any();
        }

        private bool DataIsFromOnlyV(IDataObject data)
        {
            if (data.GetDataPresent(DataFormats.StringFormat))
            {
                var s = (string)data.GetData(DataFormats.StringFormat);
                if (s != null && s.StartsWith("OnlyV|", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private string GetOnlyVTitle(IDataObject data)
        {
            var s = (string)data.GetData(DataFormats.StringFormat);
            return s?.Split('|')[1];
        }

        private IEnumerable<string> GetSupportedUrls(IDataObject data)
        {
            var result = new List<string>();

            if (data.GetDataPresent(DataFormats.StringFormat))
            {
                var s = (string)data.GetData(DataFormats.StringFormat);

                if (!string.IsNullOrEmpty(s))
                {
                    using (var reader = new StringReader(s))
                    {
                        var line = reader.ReadLine();
                        if (!string.IsNullOrEmpty(line))
                        {
                            if (IsAcceptableUri(line))
                            {
                                result.Add(line.Trim());
                            }
                        }
                    }
                }
            }

            return result;
        }

        private bool IsAcceptableUri(string uri)
        {
            return 
                Uri.TryCreate(uri, UriKind.Absolute, out var uriResult) && 
                (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }

        private IEnumerable<string> GetSupportedFiles(IDataObject data)
        {
            var result = new List<string>();

            if (data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file...
                string[] files = (string[])data.GetData(DataFormats.FileDrop);

                if (files == null || !files.Any())
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

            Log.Logger.Verbose($"Found {result.Count} supported files in drag-and-drop operation");

            result.Sort();

            return result;
        }

        private string GetSupportedFile(string file)
        {
            var ext = Path.GetExtension(file);
            if (string.IsNullOrEmpty(ext) || !_mediaProviderService.IsFileExtensionSupported(ext))
            {
                return null;
            }

            return file;
        }

        private void OnCopyingFilesProgressEvent(FilesCopyProgressEventArgs e)
        {
            CopyingFilesProgressEvent?.Invoke(this, e);
        }
    }
}
