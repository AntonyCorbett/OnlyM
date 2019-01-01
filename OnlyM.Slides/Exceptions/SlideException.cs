namespace OnlyM.Slides.Exceptions
{
    using System;

    public class SlideException : Exception
    {
        public SlideException(string message, Exception innerException = null)
        : base(message, innerException)
        {
        }
    }
}
