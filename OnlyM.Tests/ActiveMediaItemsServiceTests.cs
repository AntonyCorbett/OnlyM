using OnlyM.Core.Models;
using OnlyM.Services.MediaChanging;

namespace OnlyM.Tests;

public class ActiveMediaItemsServiceTests
{
    [Fact]
    public void Add_AddsMediaItem_ExistsReturnsTrue()
    {
        var service = new ActiveMediaItemsService();
        var id = Guid.NewGuid();

        service.Add(id, MediaClassification.Image);

        Assert.True(service.Exists(id));
    }

    [Fact]
    public void Remove_RemovesMediaItem_ExistsReturnsFalse()
    {
        var service = new ActiveMediaItemsService();
        var id = Guid.NewGuid();

        service.Add(id, MediaClassification.Video);
        service.Remove(id);

        Assert.False(service.Exists(id));
    }

    [Fact]
    public void Exists_ReturnsFalse_WhenItemNotPresent()
    {
        var service = new ActiveMediaItemsService();
        var id = Guid.NewGuid();

        Assert.False(service.Exists(id));
    }

    [Fact]
    public void Any_WithClassifications_ReturnsTrue_IfAnyMatch()
    {
        var service = new ActiveMediaItemsService();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        service.Add(id1, MediaClassification.Audio);
        service.Add(id2, MediaClassification.Image);

        Assert.True(service.Any(MediaClassification.Image, MediaClassification.Video));
    }

    [Fact]
    public void Any_WithClassifications_ReturnsFalse_IfNoneMatch()
    {
        var service = new ActiveMediaItemsService();
        var id = Guid.NewGuid();

        service.Add(id, MediaClassification.Web);

        Assert.False(service.Any(MediaClassification.Image, MediaClassification.Video));
    }

    [Fact]
    public void Any_NoParams_ReturnsTrue_IfAnyItems()
    {
        var service = new ActiveMediaItemsService();
        service.Add(Guid.NewGuid(), MediaClassification.Slideshow);

        Assert.True(service.Any());
    }

    [Fact]
    public void Any_NoParams_ReturnsFalse_IfNoItems()
    {
        var service = new ActiveMediaItemsService();

        Assert.False(service.Any());
    }

    [Fact]
    public void GetMediaItemIds_ReturnsAllIds()
    {
        var service = new ActiveMediaItemsService();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        service.Add(id1, MediaClassification.Image);
        service.Add(id2, MediaClassification.Video);

        var ids = service.GetMediaItemIds();

        Assert.Contains(id1, ids);
        Assert.Contains(id2, ids);
        Assert.Equal(2, ids.Count);
    }
}
