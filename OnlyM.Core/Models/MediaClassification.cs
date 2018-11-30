namespace OnlyM.Core.Models
{
    public enum MediaClassification
    {
        /// <summary>
        /// Unknown media type.
        /// </summary>
        Unknown,

        /// <summary>
        /// An image file.
        /// </summary>
        Image,

        /// <summary>
        /// A video file.
        /// </summary>
        Video,

        /// <summary>
        /// An audio file.
        /// </summary>
        Audio,

        /// <summary>
        /// A slideshow file.
        /// </summary>
        Slideshow,

        /// <summary>
        /// A web address shortcut (*.url)
        /// </summary>
        Web
    }
}
