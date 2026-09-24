using System.Collections.Generic;

namespace OnlyM.PubSubMessages;

internal sealed class ExternalDropCompletedMessage
{
    public string MediaFolder { get; init; } = string.Empty;

    public int TargetIndex { get; init; }

    public string? TargetFilePath { get; init; }

    public IReadOnlyList<string> CopiedFilePaths { get; init; } = [];
}
