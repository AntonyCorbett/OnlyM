namespace OnlyM.Services.StartOffsetStorage
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using OnlyM.Core.Services.Database;
    using Serilog;

    internal class StartOffsetStorageService : IStartOffsetStorageService
    {
        private readonly IDatabaseService _databaseService;

        public StartOffsetStorageService(IDatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public IReadOnlyCollection<int> ReadOffsets(string mediaFileName, int mediaDurationSeconds)
        {
            var result = new List<int>();

            MediaStartOffsetData data = null;

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
                return result;
            }

            if (data.LengthSeconds != mediaDurationSeconds)
            {
                // file may have changed...
                return result;
            }

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

        public void Store(string mediaFileName, int mediaDurationSeconds, IReadOnlyCollection<int> recentTimes)
        {
            try
            {
                var timesAsString = recentTimes == null || !recentTimes.Any()
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
}
