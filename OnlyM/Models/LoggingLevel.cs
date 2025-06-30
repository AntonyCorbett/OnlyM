using Serilog.Events;

namespace OnlyM.Models;

internal sealed class LoggingLevel
{
    public string? Name { get; init; }

    public LogEventLevel Level { get; init; }
}
