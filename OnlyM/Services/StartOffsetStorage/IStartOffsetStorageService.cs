namespace OnlyM.Services.StartOffsetStorage
{
    using System.Collections.Generic;

    internal interface IStartOffsetStorageService
    {
        IReadOnlyCollection<int> ReadOffsets(string mediaFileName, int mediaDurationSeconds);

        void Store(string mediaFileName, int mediaDurationSeconds, IReadOnlyCollection<int> recentTimes);
    }
}
