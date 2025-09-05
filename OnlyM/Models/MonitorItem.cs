using System.Text;
using System.Windows.Forms;
using OnlyM.Core.Models;
using OnlyM.Properties;

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
        Primary = sm.Monitor?.Primary ?? false;
    }

    public Screen? Monitor { get; init; }

    public string? MonitorName { get; init; }

    public string? MonitorId { get; init; }

    public string? FriendlyName { get; init; }

    public bool Primary { get; }

    public string NameForDisplayInUI
    {
        get
        {
            var sb = new StringBuilder(FriendlyName);
            if (Primary)
            {
                sb.Append(" (");
                sb.Append(Resources.PRIMARY_MONITOR);
                sb.Append(')');
            }

            return sb.ToString();
        }
    }
}
