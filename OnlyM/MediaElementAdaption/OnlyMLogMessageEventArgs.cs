using Serilog.Events;

namespace OnlyM.MediaElementAdaption
{
    internal class OnlyMLogMessageEventArgs
    {
        public OnlyMLogMessageEventArgs(LogEventLevel level, string message)
        {
            Level = level;
            Message = message;
        }

        public string Message { get; set; }

        public LogEventLevel Level { get; set; }
    }
}
