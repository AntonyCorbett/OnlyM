namespace OnlyM.Models
{
    using System;

    public class NewMediaSizeEventArgs : EventArgs
    {
        public int Width { get; set; }

        public int Height { get; set; }
    }
}
