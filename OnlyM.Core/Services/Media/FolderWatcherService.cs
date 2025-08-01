﻿using System;
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
    private readonly ManualResetEventSlim _signalFolderChange = new(false);
    private FileSystemWatcher? _watcher;
    private int _changeVersion;
    private MediaFolders? _foldersToWatch;

    public FolderWatcherService(IOptionsService optionsService, IMediaProviderService mediaProviderService)
    {
        _mediaProviderService = mediaProviderService;

        _optionsService = optionsService;
        _optionsService.MediaFolderChangedEvent += HandleMediaFolderChangedEvent;
        _optionsService.OperatingDateChangedEvent += HandleOperatingDateChangedEvent;

        Task.Run(CollationFunction);

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
        _signalFolderChange.Dispose();
        _watcher?.Dispose();
    }

    private Task CollationFunction()
    {
        var currentChangeVersion = _changeVersion;

        while (true)
        {
            _signalFolderChange.Wait();

            // some change activity.
            while (_changeVersion > currentChangeVersion)
            {
                // delay until no further changes for at least 500ms.
                currentChangeVersion = _changeVersion;
                Thread.Sleep(500);
            }

            try
            {
                OnChangesFoundEvent();
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Folder watcher collation");
            }

            _signalFolderChange.Reset();
        }

        // ReSharper disable once FunctionNeverReturns
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
            // not a relevant file.
            return;
        }

        if (!IsWatchingFilesFolder(e.OldFullPath) && !IsWatchingFilesFolder(e.FullPath))
        {
            return;
        }

        Interlocked.Increment(ref _changeVersion);
        _signalFolderChange.Set();
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
                    // not a relevant file.
                    return;
                }

                break;
        }

        if (!IsWatchingFilesFolder(e.FullPath))
        {
            return;
        }

        Interlocked.Increment(ref _changeVersion);
        _signalFolderChange.Set();
    }

    private void HandleMediaFolderChangedEvent(object? sender, EventArgs e) =>
        // Main Media Folder has changed.
        InitWatcher();

    private void HandleOperatingDateChangedEvent(object? sender, EventArgs e) =>
        // Operating date has changed (so we may need to watch
        // a different Calendar folder).
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
