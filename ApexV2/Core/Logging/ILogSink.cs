namespace ApexV2.Core.Logging;

public interface ILogSink
{
    ValueTask WriteAsync(LogEvent logEvent, CancellationToken ct = default);
}
