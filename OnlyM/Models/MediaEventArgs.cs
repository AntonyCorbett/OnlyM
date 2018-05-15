namespace OnlyM.Models
{
    using System;

    public class MediaEventArgs : EventArgs
    {
        public Guid MediaItemId { get; set; }

        public MediaChange Change { get; set; }
    }
}
