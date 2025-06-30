using System.Windows.Forms;

namespace OnlyM.Core.Models;

public class SystemMonitor
{
    public Screen? Monitor { get; init; }

    public string? MonitorName { get; init; }

    public string? MonitorId { get; init; }

    public string? FriendlyName { get; set; }
}
