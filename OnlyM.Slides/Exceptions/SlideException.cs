using System;

namespace OnlyM.Slides.Exceptions;

[Serializable]
public class SlideException : Exception
{
    public SlideException()
    {
    }

    public SlideException(string message)
        : base(message)
    {
    }

    public SlideException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
