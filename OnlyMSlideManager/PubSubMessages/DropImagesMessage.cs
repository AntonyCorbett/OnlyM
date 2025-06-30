using System.Collections.Generic;

namespace OnlyMSlideManager.PubSubMessages;

internal sealed class DropImagesMessage
{
    public List<string>? FileList { get; init; }

    public string? TargetId { get; init; }
}
