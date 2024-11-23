using System.Collections.Generic;

namespace OnlyM.Services.StartOffsetStorage;

internal interface IStartOffsetStorageService
{
    IReadOnlyCollection<int> ReadOffsets(string mediaFileName, int mediaDurationSeconds);

    void Store(string mediaFileName, int mediaDurationSeconds, IReadOnlyCollection<int>? recentTimes);
}
