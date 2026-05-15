using OnlyM.Core.Models;

namespace OnlyM.Models;

internal sealed class AppModeItem
{
    public string? Name { get; init; }

    public DarkModeOption Mode { get; init; }
}
