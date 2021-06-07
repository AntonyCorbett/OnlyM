using Serilog.Events;

namespace OnlyM.Core.Services.Options
{
    public interface ILogLevelSwitchService
    {
        void SetMinimumLevel(LogEventLevel level);
    }
}
