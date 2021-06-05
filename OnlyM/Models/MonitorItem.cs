using OnlyM.Core.Models;

namespace OnlyM.Models
{
    using System.Windows.Forms;

    internal class MonitorItem
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

        public Screen Monitor { get; set; }

        public string MonitorName { get; set; }

        public string MonitorId { get; set; }

        public string FriendlyName { get; set; }
    }
}
