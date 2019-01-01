namespace OnlyM.Slides.Exceptions
{
    using System;

    public class SlideWithNameExistsException : SlideException
    {
        public SlideWithNameExistsException(string existingSlideName, Exception innerException = null) 
            : base($"Slide '{existingSlideName}' already exists", innerException)
        {
            ExistingSlideName = existingSlideName;
        }

        public string ExistingSlideName { get; }
    }
}
