namespace OnlyM.Models
{
    public enum MediaChange
    {
        /// <summary>
        /// Unknown change.
        /// </summary>
        Unknown,

        /// <summary>
        /// Media is stopping.
        /// </summary>
        Stopping,

        /// <summary>
        /// Media is starting.
        /// </summary>
        Starting,

        /// <summary>
        /// Media has stopped.
        /// </summary>
        Stopped,

        /// <summary>
        /// Media has started.
        /// </summary>
        Started,

        /// <summary>
        /// Media is paused.
        /// </summary>
        Paused,
    }
}
