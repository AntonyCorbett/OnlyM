using System.Collections.Generic;

namespace OnlyMSlideManager.PubSubMessages;

internal sealed class DropImagesMessage
{
    public List<string>? FileList { get; set; }

    public string? TargetId { get; set; }
}
