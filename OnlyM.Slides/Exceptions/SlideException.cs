using System;

namespace OnlyM.Slides.Exceptions
{
    [Serializable]
    public class SlideException : Exception
    {
        public SlideException() 
            : base()
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

        protected SlideException(
            System.Runtime.Serialization.SerializationInfo serializationInfo, 
            System.Runtime.Serialization.StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }
    }
}
