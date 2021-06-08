using Serilog.Events;

namespace OnlyM.Models
{
    internal class LoggingLevel
    {
        public string? Name { get; set; }

        public LogEventLevel Level { get; set; }
    }
}
