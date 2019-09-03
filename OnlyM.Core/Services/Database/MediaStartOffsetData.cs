namespace OnlyM.Core.Services.Database
{
    using System.Collections.Generic;

    public class MediaStartOffsetData
    {
        public int Id { get; set; }

        public string FileName { get; set; }

        public int LengthSeconds { get; set; }

        public List<int> StartOffsets { get; set; }

        public void Sanitize()
        {
        }
    }
}
