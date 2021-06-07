using Serilog.Events;

namespace OnlyMSlideManager.Services.Options
{
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
            // add any model cleanup here
        }
    }
}
