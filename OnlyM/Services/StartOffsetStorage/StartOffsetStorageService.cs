using System;
using System.Collections.Generic;
using OnlyM.Core.Services.Database;
using Serilog;

namespace OnlyM.Services.StartOffsetStorage;

internal sealed class StartOffsetStorageService : IStartOffsetStorageService
{
    private readonly IDatabaseService _databaseService;

    public StartOffsetStorageService(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public IReadOnlyCollection<int> ReadOffsets(string mediaFileName, int mediaDurationSeconds)
    {
        MediaStartOffsetData? data = null;

        try
        {
            data = _databaseService.GetMediaStartOffsetData(mediaFileName);
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Could not get start offset data from database");
        }

        if (data?.StartOffsets == null)
        {
            return [];
        }

        if (data.LengthSeconds != mediaDurationSeconds)
        {
            // file may have changed...
            return [];
        }

        var result = new List<int>(data.StartOffsets.Count);

        foreach (var offset in data.StartOffsets)
        {
            if (offset > 0 &&
                offset < mediaDurationSeconds &&
                !result.Contains(offset))
            {
                result.Add(offset);
            }
        }

        result.Sort();

        return result;
    }

    public void Store(string mediaFileName, int mediaDurationSeconds, IReadOnlyCollection<int>? recentTimes)
    {
        try
        {
            var timesAsString = recentTimes == null || recentTimes.Count == 0
                ? string.Empty
                : string.Join(",", recentTimes);

            _databaseService.AddMediaStartOffsetData(mediaFileName, timesAsString, mediaDurationSeconds);
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Could not get store offset data in database");
        }
    }
}
