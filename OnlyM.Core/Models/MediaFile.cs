namespace OnlyM.Core.Models
{
    public class MediaFile
    {
        public string FullPath { get; set; }

        public SupportedMediaType MediaType { get; set; }

        public long LastChanged { get; set; }
    }
}
