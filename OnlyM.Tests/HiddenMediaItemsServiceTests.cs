using OnlyM.Models;
using OnlyM.Services.HiddenMediaItems;

namespace OnlyM.Tests;

public class HiddenMediaItemsServiceTests
{
    private static MediaItem CreateMediaItem(string? path, bool isVisible)
    {
        return new MediaItem
        {
            FilePath = path,
            IsVisible = isVisible
        };
    }

    [Fact]
    public void Init_HidesInvisibleItems_AddsToHidden()
    {
        var item1 = CreateMediaItem("file1", false);
        var item2 = CreateMediaItem("file2", true);
        var service = new HiddenMediaItemsService();

        var eventRaised = false;
        service.HiddenItemsChangedEvent += (_, _) => eventRaised = true;

        service.Init([item1, item2]);

        Assert.True(service.SomeHiddenMediaItems());
        Assert.True(eventRaised);
    }

    [Fact]
    public void Init_VisibleItemAlreadyHidden_MakesInvisible()
    {
        var item1 = CreateMediaItem("file1", false);
        var service = new HiddenMediaItemsService();

        service.Init([item1]);
        Assert.True(service.SomeHiddenMediaItems());

        // Now re-init with the same file, but visible
        var item2Mock = CreateMediaItem("file1", true);
        service.Init([item2Mock]);

        Assert.False(item2Mock.IsVisible);
    }

    [Fact]
    public void Add_AddsPathToHidden()
    {
        var service = new HiddenMediaItemsService();
        var eventRaised = false;
        service.HiddenItemsChangedEvent += (_, _) => eventRaised = true;

        service.Add("file1");

        Assert.True(service.SomeHiddenMediaItems());
        Assert.True(eventRaised);
    }

    [Fact]
    public void Remove_RemovesPathFromHidden()
    {
        var service = new HiddenMediaItemsService();
        service.Add("file1");
        var eventRaised = false;
        service.HiddenItemsChangedEvent += (_, _) => eventRaised = true;

        service.Remove("file1");

        Assert.False(service.SomeHiddenMediaItems());
        Assert.True(eventRaised);
    }

    [Fact]
    public void SomeHiddenMediaItems_ReturnsFalseWhenNone()
    {
        var service = new HiddenMediaItemsService();
        Assert.False(service.SomeHiddenMediaItems());
    }

    [Fact]
    public void UnhideAllMediaItems_ClearsAllHiddenItemsAndRaisesEvents()
    {
        var service = new HiddenMediaItemsService();
        service.Add("file1");
        service.Add("file2");

        var hiddenChanged = false;
        var unhideAll = false;
        service.HiddenItemsChangedEvent += (_, _) => hiddenChanged = true;
        service.UnhideAllEvent += (_, _) => unhideAll = true;

        service.UnhideAllMediaItems();

        Assert.False(service.SomeHiddenMediaItems());
        Assert.True(hiddenChanged);
        Assert.True(unhideAll);
    }
}
