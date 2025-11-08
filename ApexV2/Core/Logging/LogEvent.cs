using System.Diagnostics;

namespace ApexV2.Core.Logging;

public sealed class LogEvent
{
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    public LogLevel Level { get; init; }
    public string Category { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public Exception? Exception { get; init; }
    public int ThreadId { get; init; } = Environment.CurrentManagedThreadId;
    public ActivityTraceId? TraceId { get; init; }
    public ActivitySpanId? SpanId { get; init; }
    public Dictionary<string, object?> Properties { get; init; } = new();

    public override string ToString()
    {
        var ex = Exception == null ? string.Empty : $" | EX: {Exception.GetType().Name}: {Exception.Message}";
        return $"[{TimestampUtc:O}] [{Level}] [{Category}] {Message}{ex}";
    }
}
