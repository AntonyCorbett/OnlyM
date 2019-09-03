namespace OnlyM.Models
{
    using System;
    using OnlyM.Core.Extensions;

    internal class RecentTimesItem
    {
        public int Seconds { get; set; }

        public string AsString => ToString();

        public bool IsNotZero => Seconds != 0;

        public override string ToString()
        {
            if (Seconds == 0)
            {
                return Properties.Resources.CLEAR_RECENTS;
            }

            return TimeSpan.FromSeconds(Seconds).AsMediaDurationString();
        }
    }
}
