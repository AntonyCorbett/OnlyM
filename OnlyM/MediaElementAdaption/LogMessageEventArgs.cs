namespace OnlyM.MediaElementAdaption
{
    using Serilog.Events;

    internal class LogMessageEventArgs
    {
        public LogMessageEventArgs(LogEventLevel level, string message)
        {
            Level = level;
            Message = message;
        }

        public string Message { get; set; }

        public LogEventLevel Level { get; set; }
    }
}
