using System;
using System.Collections.Generic;
using OnlyM.Models;

namespace OnlyM.Services.HiddenMediaItems
{
    internal sealed class HiddenMediaItemsService : IHiddenMediaItemsService
    {
        private readonly HashSet<string> _allHiddenItems = new();

        private readonly HashSet<string> _hiddenItemsInCurrentMediaFolder = new();

        public event EventHandler? HiddenItemsChangedEvent;

        public event EventHandler? UnhideAllEvent;

        public void Init(IEnumerable<MediaItem> items)
        {
            _hiddenItemsInCurrentMediaFolder.Clear();

            foreach (var item in items)
            {
                if (item.FilePath == null)
                {
                    continue;
                }

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

        public bool SomeHiddenMediaItems() => _hiddenItemsInCurrentMediaFolder.Count > 0;
        
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

        private void OnHiddenItemsChangedEvent()
        {
            HiddenItemsChangedEvent?.Invoke(this, EventArgs.Empty);
        }
    }
}
