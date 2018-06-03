namespace OnlyM.Services.RecentMediaFolders
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal class RecentlyUsedMediaFolderService : IRecentlyUsedMediaFolderService
    {
        private const int MaxCount = 10;

        private List<string> _recentlyUsedFolders;

        public void Init(List<string> recentlyUsedFolders)
        {
            _recentlyUsedFolders = recentlyUsedFolders;
        }
        
        public void Add(string folder)
        {
            bool exists = IsInList(folder);

            if (exists)
            {
                // first remove so we can append to the
                // end of the list (most recently used).
                Remove(folder);
            }

            TrimList();
            _recentlyUsedFolders.Add(folder);
        }

        private void TrimList()
        {
            while (_recentlyUsedFolders.Count >= MaxCount)
            {
                _recentlyUsedFolders.RemoveAt(0);
            }
        }

        private void Remove(string folder)
        {
            _recentlyUsedFolders.RemoveAll(x => x.Equals(folder, StringComparison.OrdinalIgnoreCase));
        }

        private bool IsInList(string folder)
        {
            return _recentlyUsedFolders.Any(x => x.Equals(folder, StringComparison.OrdinalIgnoreCase));
        }
    }
}
