using OnlyM.Core.Services.Options;

namespace OnlyM.Models;

internal sealed class RemoteControlModeItem
{
    public RemoteControlModeItem(RemoteControlMode mode, string name)
    {
        Mode = mode;
        Name = name;
    }

    public RemoteControlMode Mode { get; }

    public string Name { get; }
}