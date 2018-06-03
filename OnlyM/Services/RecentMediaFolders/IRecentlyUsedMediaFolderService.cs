namespace OnlyM.Services.RecentMediaFolders
{
    using System.Collections.Generic;

    internal interface IRecentlyUsedMediaFolderService
    {
        void Init(List<string> optionsRecentlyUsedMediaFolders);

        void Add(string folder);
    }
}
