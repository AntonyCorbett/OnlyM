using System;

namespace OnlyM.Core.Services.Media;

[Serializable]
public class VideoFileInUseException : Exception
{
    public VideoFileInUseException()
        : base()
    {
    }

    public VideoFileInUseException(string message)
        : base(message)
    {
    }

    public VideoFileInUseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
