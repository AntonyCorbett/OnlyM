namespace OnlyMSlideManager.Services.Options
{
    using Serilog.Events;

    public sealed class Options
    {
        public Options()
        {
            // defaults
            LogEventLevel = LogEventLevel.Information;

            Sanitize();
        }

        public string AppWindowPlacement { get; set; }

        public string Culture { get; set; }

        public LogEventLevel LogEventLevel { get; set; }

        public void Sanitize()
        {
        }
    }
}
