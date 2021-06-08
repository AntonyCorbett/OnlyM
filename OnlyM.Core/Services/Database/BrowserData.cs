namespace OnlyM.Core.Services.Database
{
    public class BrowserData
    {
        private const double MaxZoomLevel = 10;
        private const double MinZoomLevel = -10;

        public int Id { get; set; }

        public string? Url { get; set; }

        public double ZoomLevel { get; set; }

        public void Sanitize()
        {
            if (ZoomLevel > MaxZoomLevel)
            {
                ZoomLevel = MaxZoomLevel;
            }

            if (ZoomLevel < MinZoomLevel)
            {
                ZoomLevel = MinZoomLevel;
            }
        }
    }
}
