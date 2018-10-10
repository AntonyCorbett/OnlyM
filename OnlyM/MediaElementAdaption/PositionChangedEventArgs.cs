namespace OnlyM.MediaElementAdaption
{
    using System;

    public class PositionChangedEventArgs : EventArgs
    {
        public PositionChangedEventArgs(Guid mediaItemId, TimeSpan position)
        {
            MediaItemId = mediaItemId;
            Position = position;
        }

        public Guid MediaItemId { get; set; }

        public TimeSpan Position { get; }
    }
}
