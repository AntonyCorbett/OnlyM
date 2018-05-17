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
    using Serilog;

    // ReSharper disable once ClassNeverInstantiated.Global
    internal class DragAndDropService : IDragAndDropService
    {
        private readonly IMediaProviderService _mediaProviderService;
        private readonly IOptionsService _optionsService;
        private bool _canDrop;

        public DragAndDropService(
            IMediaProviderService mediaProviderService,
            IOptionsService optionsService)
        {
            _mediaProviderService = mediaProviderService;
            _optionsService = optionsService;
        }

        public void Init(FrameworkElement targetElement)
        {
            targetElement.DragEnter += HandleDragEnter;
            targetElement.DragOver += HandleDragOver;
            targetElement.Drop += HandleDrop;
        }

        private void HandleDragOver(object sender, DragEventArgs e)
        {
            SetEffects(e);
            e.Handled = true;
        }

        private void HandleDrop(object sender, DragEventArgs e)
        {
            CopyMediaFiles(e.Data);
        }

        private void HandleDragEnter(object sender, DragEventArgs e)
        {
            // do we allow drop of drag object?
            _canDrop = CanDropOrPaste(e.Data);
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
                Task.Run(() => { InternalCopyMediaFiles(data); });
            }
        }

        private void InternalCopyMediaFiles(IDataObject data)
        {
            try
            {
                var mediaFolder = _optionsService.Options.MediaFolder;

                IEnumerable<string> files = GetSupportedFiles(data);

                foreach (var file in files)
                {
                    var filename = Path.GetFileName(file);
                    if (!string.IsNullOrEmpty(filename))
                    {
                        var destFile = Path.Combine(mediaFolder, filename);
                        if (!File.Exists(destFile))
                        {
                            File.Copy(file, destFile, false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Could not copy media files");
            }
        }

        private bool CanDropOrPaste(IDataObject data)
        {
            IEnumerable<string> files = GetSupportedFiles(data);
            return files.Any();
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
                    var ext = Path.GetExtension(file);
                    if (!string.IsNullOrEmpty(ext) && _mediaProviderService.IsFileExtensionSupported(ext))
                    {
                        result.Add(file);
                    }
                }
            }

            Log.Logger.Verbose($"Found {result.Count} supported files in drag-and-drop operation");

            return result;
        }
    }
}
