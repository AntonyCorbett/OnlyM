using OnlyM.Core.Services.Database;

namespace OnlyM.Core.Tests;

/// <summary>
/// Integration tests for the manual media-ordering methods on DatabaseService.
/// Each test uses a unique scopeKey so runs are fully isolated from real user data.
/// Cleanup is performed in Dispose().
/// </summary>
public sealed class DatabaseServiceMediaOrderTests : IDisposable
{
    private readonly DatabaseService _sut = new();
    private readonly string _scope = $"test-order-{Guid.NewGuid()}";

    public void Dispose()
    {
        // Remove all entries written by this test run.
        _sut.RemoveMissingMediaOrderItems(_scope, []);
    }

    [Fact]
    public void GetMediaOrderItemKeys_ReturnsEmpty_WhenNoOrderStored()
    {
        var result = _sut.GetMediaOrderItemKeys(_scope);

        Assert.Empty(result);
    }

    [Fact]
    public void UpsertMediaOrder_StoresKeysInCorrectOrder()
    {
        var keys = new List<string> { "file-c.mp4", "file-a.mp4", "file-b.mp4" };

        _sut.UpsertMediaOrder(_scope, keys);
        var result = _sut.GetMediaOrderItemKeys(_scope);

        Assert.Equal(keys, result);
    }

    [Fact]
    public void UpsertMediaOrder_UpdatesSortIndex_WhenCalledAgainWithSameKeys()
    {
        // Initial insert.
        _sut.UpsertMediaOrder(_scope, ["a.jpg", "b.jpg", "c.jpg"]);

        // Reverse the order – UpsertMediaOrder updates sortIndex for existing keys.
        _sut.UpsertMediaOrder(_scope, ["c.jpg", "b.jpg", "a.jpg"]);
        var result = _sut.GetMediaOrderItemKeys(_scope);

        Assert.Equal(["c.jpg", "b.jpg", "a.jpg"], result);
    }

    [Fact]
    public void RemoveMissingMediaOrderItems_RemovesEntriesNotInProvidedSet()
    {
        _sut.UpsertMediaOrder(_scope, ["keep.mp4", "remove.mp4", "keep2.mp4"]);

        _sut.RemoveMissingMediaOrderItems(_scope, ["keep.mp4", "keep2.mp4"]);
        var result = _sut.GetMediaOrderItemKeys(_scope);

        Assert.Equal(["keep.mp4", "keep2.mp4"], result.Order().ToList());
        Assert.DoesNotContain("remove.mp4", result);
    }

    [Fact]
    public void RemoveMissingMediaOrderItems_ClearsAllEntries_WhenEmptySetProvided()
    {
        _sut.UpsertMediaOrder(_scope, ["a.jpg", "b.jpg"]);

        _sut.RemoveMissingMediaOrderItems(_scope, []);
        var result = _sut.GetMediaOrderItemKeys(_scope);

        Assert.Empty(result);
    }
}
