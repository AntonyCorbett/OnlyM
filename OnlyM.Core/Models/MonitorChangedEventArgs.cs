namespace OnlyM.Core.Models
{
    using System;

    public class MonitorChangedEventArgs : EventArgs
    {
        public MonitorChangeDescription Change { get; set; }
    }
}
