namespace OnlyM.Services.HiddenMediaItems
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using OnlyM.Models;

    internal class HiddenMediaItemsService : IHiddenMediaItemsService
    {
        private readonly HashSet<string> _allHiddenItems = new HashSet<string>();

        private readonly HashSet<string> _hiddenItemsInCurrentMediaFolder = new HashSet<string>();

        public event EventHandler HiddenItemsChangedEvent;

        public event EventHandler UnhideAllEvent;

        public void Init(IEnumerable<MediaItem> items)
        {
            _hiddenItemsInCurrentMediaFolder.Clear();

            foreach (var item in items)
            {
                if (!item.IsVisible)
                {
                    _hiddenItemsInCurrentMediaFolder.Add(item.FilePath);
                    _allHiddenItems.Add(item.FilePath);
                }
                else if (_allHiddenItems.Contains(item.FilePath))
                {
                    _hiddenItemsInCurrentMediaFolder.Add(item.FilePath);
                    item.IsVisible = false;
                }
            }

            OnHiddenItemsChangedEvent();
        }

        public void UnhideAllMediaItems()
        {
            foreach (var item in _hiddenItemsInCurrentMediaFolder)
            {
                _allHiddenItems.Remove(item);
            }

            _hiddenItemsInCurrentMediaFolder.Clear();

            UnhideAllEvent?.Invoke(this, EventArgs.Empty);

            OnHiddenItemsChangedEvent();
        }

        public bool SomeHiddenMediaItems()
        {
            return _hiddenItemsInCurrentMediaFolder.Any();
        }

        public void Add(string path)
        {
            _hiddenItemsInCurrentMediaFolder.Add(path);
            _allHiddenItems.Add(path);
            OnHiddenItemsChangedEvent();
        }

        public void Remove(string path)
        {
            _hiddenItemsInCurrentMediaFolder.Remove(path);
            _allHiddenItems.Remove(path);
            OnHiddenItemsChangedEvent();
        }

        protected virtual void OnHiddenItemsChangedEvent()
        {
            HiddenItemsChangedEvent?.Invoke(this, EventArgs.Empty);
        }
    }
}
