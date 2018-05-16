namespace OnlyM.Core.Services.Media
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Options;
    using Serilog;

    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed class FolderWatcherService : IFolderWatcherService
    {
        private readonly IOptionsService _optionsService;
        private readonly IMediaProviderService _mediaProviderService;
        private readonly ManualResetEventSlim _signalFolderChange = new ManualResetEventSlim(false);
        private FileSystemWatcher _watcher;
        private int _changeVersion;

        public event EventHandler ChangesFoundEvent;

        public FolderWatcherService(IOptionsService optionsService, IMediaProviderService mediaProviderService)
        {
            _mediaProviderService = mediaProviderService;

            _optionsService = optionsService;
            _optionsService.MediaFolderChangedEvent += HandleMediaFolderChangedEvent;

            Task.Run(CollationFunction);

            InitWatcher(_optionsService.Options.MediaFolder);
        }

        private Task CollationFunction()
        {
            var currentChangeVersion = _changeVersion;
            
            for (;;)
            {
                _signalFolderChange.Wait();

                // some change activity.
                while (_changeVersion > currentChangeVersion)
                {
                    // delay until no further changes for at least 500ms.
                    currentChangeVersion = _changeVersion;
                    Thread.Sleep(500);
                }

                OnChangesFoundEvent();
                _signalFolderChange.Reset();
            }

            // ReSharper disable once FunctionNeverReturns
        }

        public bool IsEnabled
        {
            get => _watcher?.EnableRaisingEvents ?? false;
            set
            {
                if (_watcher != null && _watcher.EnableRaisingEvents != value)
                {
                    if (value && !Directory.Exists(_optionsService.Options.MediaFolder))
                    {
                        return;
                    }

                    _watcher.EnableRaisingEvents = value;
                }
            }
        }

        private void InitWatcher(string pathToWatch)
        {
            if (_watcher == null)
            {
                _watcher = new FileSystemWatcher();
                _watcher.Created += HandleContentModified;
                _watcher.Deleted += HandleContentModified;
                _watcher.Renamed += HandleContentModified;
            }

            if (Directory.Exists(pathToWatch))
            {
                _watcher.Path = pathToWatch;
                _watcher.EnableRaisingEvents = true;
            }
            else
            {
                _watcher.EnableRaisingEvents = false;
            }
        }

        private void HandleContentModified(object sender, FileSystemEventArgs e)
        {
            switch (e.ChangeType)
            {
                case WatcherChangeTypes.Created:
                case WatcherChangeTypes.Deleted:
                    if (!_mediaProviderService.IsFileExtensionSupported(Path.GetExtension(e.FullPath)))
                    {
                        // not a relevant file.
                        return;
                    }

                    break;
            }

            Interlocked.Increment(ref _changeVersion);
            _signalFolderChange.Set();
        }

        private void HandleMediaFolderChangedEvent(object sender, EventArgs e)
        {
            InitWatcher(_optionsService.Options.MediaFolder);
        }

        private void OnChangesFoundEvent()
        {
            Log.Logger.Verbose("Folder changes");
            ChangesFoundEvent?.Invoke(this, EventArgs.Empty);
        }
    }
}
