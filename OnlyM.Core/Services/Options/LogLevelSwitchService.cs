namespace OnlyM.Core.Services.Options
{
    using Serilog.Core;
    using Serilog.Events;

    // ReSharper disable once ClassNeverInstantiated.Global
    public class LogLevelSwitchService : ILogLevelSwitchService
    {
        public static readonly LoggingLevelSwitch LevelSwitch = new LoggingLevelSwitch
        {
            MinimumLevel = LogEventLevel.Information,
        };

        public void SetMinimumLevel(LogEventLevel level)
        {
            LevelSwitch.MinimumLevel = level;
        }
    }
}
