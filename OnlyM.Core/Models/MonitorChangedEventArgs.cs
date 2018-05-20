namespace OnlyM.Core.Models
{
    using System;

    public class MonitorChangedEventArgs : EventArgs
    {
        public string OriginalMonitorId { get; set; }

        public string NewMonitorId { get; set; }
    }
}
