namespace OnlyM.MediaElementAdaption
{
    using System;

    public class PositionChangedEventArgs : EventArgs
    {
        public Guid MediaItemId { get; set; }

        public TimeSpan Position { get; }
        
        public TimeSpan OldPosition { get; }

        public PositionChangedEventArgs(Guid mediaItemId, TimeSpan oldPosition, TimeSpan position)
        {
            MediaItemId = mediaItemId;
            OldPosition = oldPosition;
            Position = position;
        }
    }
}
