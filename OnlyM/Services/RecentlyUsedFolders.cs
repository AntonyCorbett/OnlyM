using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace OnlyM.Services
{
    internal class RecentlyUsedFolders
    {
        private const int MaxCount = 10;

        private readonly ObservableCollection<string> _recentlyUsedFolders = new();

        public void Add(IEnumerable<string> recentlyUsedFolders)
        {
            foreach (var folder in recentlyUsedFolders)
            {
                Add(folder);
            }
        }
        
        public void Add(string folder)
        {
            var exists = IsInList(folder);

            if (exists)
            {
                // first remove so we can insert at head 
                // of the list (most recently used).
                Remove(folder);
            }

            TrimList();
            _recentlyUsedFolders.Insert(0, folder);
        }

        public ObservableCollection<string> GetFolders()
        {
            return _recentlyUsedFolders;
        }

        private void TrimList()
        {
            while (_recentlyUsedFolders.Count >= MaxCount)
            {
                _recentlyUsedFolders.RemoveAt(MaxCount - 1);
            }
        }

        private void Remove(string folder)
        {
            _recentlyUsedFolders.Remove(folder);
        }

        private bool IsInList(string folder)
        {
            return _recentlyUsedFolders.Contains(folder);
        }
    }
}
