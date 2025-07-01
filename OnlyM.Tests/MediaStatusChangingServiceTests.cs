using OnlyM.Services.MediaChanging;

namespace OnlyM.Tests;

public class MediaStatusChangingServiceTests
{
    [Fact]
    public void AddChangingItem_ItemAdded_IsMediaStatusChangingReturnsTrue()
    {
        // Arrange
        var service = new MediaStatusChangingService();
        var id = Guid.NewGuid();

        // Act
        service.AddChangingItem(id);

        // Assert
        Assert.True(service.IsMediaStatusChanging());
    }

    [Fact]
    public void RemoveChangingItem_ItemRemoved_IsMediaStatusChangingReturnsFalse()
    {
        // Arrange
        var service = new MediaStatusChangingService();
        var id = Guid.NewGuid();
        service.AddChangingItem(id);

        // Act
        service.RemoveChangingItem(id);

        // Assert
        Assert.False(service.IsMediaStatusChanging());
    }

    [Fact]
    public void IsMediaStatusChanging_NoItems_ReturnsFalse()
    {
        // Arrange
        var service = new MediaStatusChangingService();

        // Act & Assert
        Assert.False(service.IsMediaStatusChanging());
    }

    [Fact]
    public void AddChangingItem_SameItemTwice_IsMediaStatusChangingStillTrue()
    {
        // Arrange
        var service = new MediaStatusChangingService();
        var id = Guid.NewGuid();

        // Act
        service.AddChangingItem(id);
        service.AddChangingItem(id);

        // Assert
        Assert.True(service.IsMediaStatusChanging());
    }

    [Fact]
    public void RemoveChangingItem_ItemNotPresent_DoesNotThrow()
    {
        // Arrange
        var service = new MediaStatusChangingService();
        var id = Guid.NewGuid();

        // Act & Assert
        var exception = Record.Exception(() => service.RemoveChangingItem(id));
        Assert.Null(exception);
    }

    [Fact]
    public void AddAndRemoveMultipleItems_IsMediaStatusChangingReflectsState()
    {
        // Arrange
        var service = new MediaStatusChangingService();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        // Act
        service.AddChangingItem(id1);
        service.AddChangingItem(id2);
        Assert.True(service.IsMediaStatusChanging());

        service.RemoveChangingItem(id1);
        Assert.True(service.IsMediaStatusChanging());

        service.RemoveChangingItem(id2);
        Assert.False(service.IsMediaStatusChanging());
    }
}
