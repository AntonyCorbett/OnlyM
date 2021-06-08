using System.Collections.Generic;

namespace OnlyM.Core.Services.Database
{
    public class MediaStartOffsetData
    {
        public int Id { get; set; }

        public string? FileName { get; set; }

        public int LengthSeconds { get; set; }

        public List<int>? StartOffsets { get; set; }

#pragma warning disable CA1822 // Mark members as static
#pragma warning disable U2U1002 // Mark members as static
        public void Sanitize()
#pragma warning restore U2U1002 // Mark members as static
#pragma warning restore CA1822 // Mark members as static
        {
            // sanitize model here if required
        }
    }
}
