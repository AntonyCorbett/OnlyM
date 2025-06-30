using System.Windows.Forms;
using OnlyM.Core.Models;

namespace OnlyM.Models;

internal sealed class MonitorItem
{
    public MonitorItem()
    {
    }

    public MonitorItem(SystemMonitor sm)
    {
        Monitor = sm.Monitor;
        FriendlyName = sm.FriendlyName;
        MonitorId = sm.MonitorId;
        MonitorName = sm.MonitorName;
    }

    public Screen? Monitor { get; init; }

    public string? MonitorName { get; init; }

    public string? MonitorId { get; init; }

    public string? FriendlyName { get; init; }
}
