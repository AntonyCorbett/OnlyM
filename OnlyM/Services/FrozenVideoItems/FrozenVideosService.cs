﻿using System.Collections.Generic;
using OnlyM.Models;

namespace OnlyM.Services.FrozenVideoItems;

internal sealed class FrozenVideosService : IFrozenVideosService
{
    private readonly HashSet<string> _frozenItems = [];

    public void Init(IEnumerable<MediaItem> items)
    {
        foreach (var item in items)
        {
            if (item.FilePath == null)
            {
                continue;
            }

            if (item.PauseOnLastFrame)
            {
                _frozenItems.Add(item.FilePath);
            }
            else if (_frozenItems.Contains(item.FilePath))
            {
                item.PauseOnLastFrame = true;
            }
        }
    }

    public void Add(string path) => _frozenItems.Add(path);

    public void Remove(string path) => _frozenItems.Remove(path);
}
