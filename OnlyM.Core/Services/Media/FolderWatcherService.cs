using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OnlyM.Core.Models;
using OnlyM.Core.Services.Options;
using Serilog;

namespace OnlyM.Core.Services.Media;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class FolderWatcherService : IFolderWatcherService, IDisposable
{
    private readonly IOptionsService _optionsService;
    private readonly IMediaProviderService _mediaProviderService;

    // SemaphoreSlim(0,1) acts as a one-slot async signal: Release() sets it, WaitAsync() clears it.
    private readonly SemaphoreSlim _signal = new(0, 1);
    private readonly CancellationTokenSource _collationCts = new();

    private FileSystemWatcher? _watcher;
    private int _changeVersion;
    private MediaFolders? _foldersToWatch;

    public FolderWatcherService(IOptionsService optionsService, IMediaProviderService mediaProviderService)
    {
        _mediaProviderService = mediaProviderService;

        _optionsService = optionsService;
        _optionsService.MediaFolderChangedEvent += HandleMediaFolderChangedEvent;
        _optionsService.OperatingDateChangedEvent += HandleOperatingDateChangedEvent;

        Task.Run(CollationFunctionAsync);

        InitWatcher();
    }

    public event EventHandler? ChangesFoundEvent;

    public bool IsEnabled
    {
        get => _watcher?.EnableRaisingEvents ?? false;
        set
        {
            if (_watcher == null || _watcher.EnableRaisingEvents == value)
            {
                return;
            }

            if (value && !Directory.Exists(_optionsService.MediaFolder))
            {
                return;
            }

            _watcher.EnableRaisingEvents = value;
        }
    }

    public void Dispose()
    {
        _watcher?.EnableRaisingEvents = false;

        _collationCts.Cancel();
        _collationCts.Dispose();
        _signal.Dispose();
        _watcher?.Dispose();
    }

    private async Task CollationFunctionAsync()
    {
        var currentChangeVersion = _changeVersion;
        var token = _collationCts.Token;

        while (!token.IsCancellationRequested)
        {
            try
            {
                await _signal.WaitAsync(token);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            // Debounce: keep waiting until no further changes for at least 500ms.
            while (_changeVersion > currentChangeVersion)
            {
                currentChangeVersion = _changeVersion;
                try
                {
                    await Task.Delay(500, token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            try
            {
                OnChangesFoundEvent();
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Folder watcher collation");
            }
        }
    }

    private void NotifyChange()
    {
        Interlocked.Increment(ref _changeVersion);

        // SemaphoreSlim(0,1): Release() throws SemaphoreFullException if already signaled.
        // Swallow it — the collation task will wake on the existing signal.
        try
        {
            _signal.Release();
        }
        catch (SemaphoreFullException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void InitWatcher(MediaFolders mediaFolders)
    {
        if (_watcher == null)
        {
            _watcher = new FileSystemWatcher { IncludeSubdirectories = true };

            _watcher.Created += HandleContentModified;
            _watcher.Deleted += HandleContentModified;
            _watcher.Changed += HandleContentModified;
            _watcher.Renamed += HandleContentRenamed;
        }

        if (mediaFolders.MediaFolder != null && Directory.Exists(mediaFolders.MediaFolder))
        {
            _watcher.Path = mediaFolders.MediaFolder;
            _watcher.EnableRaisingEvents = true;
        }
        else
        {
            _watcher.EnableRaisingEvents = false;
        }
    }

    private void HandleContentRenamed(object sender, RenamedEventArgs e)
    {
        if (!_mediaProviderService.IsFileExtensionSupported(Path.GetExtension(e.OldFullPath)) &&
            !_mediaProviderService.IsFileExtensionSupported(Path.GetExtension(e.FullPath)))
        {
            return;
        }

        if (!IsWatchingFilesFolder(e.OldFullPath) && !IsWatchingFilesFolder(e.FullPath))
        {
            return;
        }

        NotifyChange();
    }

    private bool IsWatchingFilesFolder(string path)
    {
        if (_foldersToWatch == null)
        {
            return false;
        }

        var directory = Path.GetDirectoryName(path);

        if (directory == null)
        {
            return false;
        }

        return
            directory.Equals(_foldersToWatch.MediaFolder, StringComparison.Ordinal) ||
            (_foldersToWatch.DatedSubFolder != null && directory.Equals(_foldersToWatch.DatedSubFolder, StringComparison.Ordinal));
    }

    private void HandleContentModified(object sender, FileSystemEventArgs e)
    {
        switch (e.ChangeType)
        {
            case WatcherChangeTypes.Created:
            case WatcherChangeTypes.Changed:
            case WatcherChangeTypes.Deleted:
                if (!_mediaProviderService.IsFileExtensionSupported(Path.GetExtension(e.FullPath)))
                {
                    return;
                }

                break;
        }

        if (!IsWatchingFilesFolder(e.FullPath))
        {
            return;
        }

        NotifyChange();
    }

    private void HandleMediaFolderChangedEvent(object? sender, EventArgs e) =>
        InitWatcher();

    private void HandleOperatingDateChangedEvent(object? sender, EventArgs e) =>
        InitWatcher();

    private void OnChangesFoundEvent()
    {
        Log.Logger.Verbose("Folder changes");
        ChangesFoundEvent?.Invoke(this, EventArgs.Empty);
    }

    private void InitWatcher()
    {
        _foldersToWatch = _mediaProviderService.GetMediaFolders(_optionsService.OperatingDate);
        InitWatcher(_foldersToWatch);
    }
}
