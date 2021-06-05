namespace OnlyM.MediaElementAdaption
{
    using System;

    public class OnlyMPositionChangedEventArgs : EventArgs
    {
        public OnlyMPositionChangedEventArgs(Guid mediaItemId, TimeSpan position)
        {
            MediaItemId = mediaItemId;
            Position = position;
        }

        public Guid MediaItemId { get; set; }

        public TimeSpan Position { get; }
    }
}
