using OnlyMSlideManager.Models;

namespace OnlyMSlideManager.PubSubMessages;

internal sealed class ReorderMessage
{
    public SlideItem? SourceItem { get; init; }

    public string? TargetId { get; init; }
}
