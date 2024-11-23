using Serilog.Events;

namespace OnlyM.Models;

internal sealed class LoggingLevel
{
    public string? Name { get; set; }

    public LogEventLevel Level { get; set; }
}
