namespace OnlyM.Core.Services.Options
{
    using Serilog.Events;

    public interface ILogLevelSwitchService
    {
        void SetMinimumLevel(LogEventLevel level);
    }
}
