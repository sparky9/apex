namespace ApexV2.Core.Logging;

public interface ILogFormatter
{
    string Format(LogEvent logEvent);
}
