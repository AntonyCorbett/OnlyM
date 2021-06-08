using System;

namespace OnlyM.MediaElementAdaption
{
    public class OnlyMMediaFailedEventArgs : EventArgs
    {
        public Exception? Error { get; set; }
    }
}
