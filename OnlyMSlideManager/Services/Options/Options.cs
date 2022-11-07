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

        public string? AppWindowPlacement { get; set; }

        public string? Culture { get; set; }

        public LogEventLevel LogEventLevel { get; set; }

#pragma warning disable CA1822 // Mark members as static
        public void Sanitize()
#pragma warning restore CA1822 // Mark members as static
        {
            // add any model cleanup here
        }
    }
}
