using System.Text.Json;

namespace ApexV2.Core.Logging;

public sealed class JsonLogFormatter : ILogFormatter
{
    private readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public string Format(LogEvent logEvent)
    {
        var obj = new
        {
            ts = logEvent.TimestampUtc,
            level = logEvent.Level.ToString(),
            cat = logEvent.Category,
            msg = logEvent.Message,
            ex = logEvent.Exception?.ToString(),
            tid = logEvent.ThreadId,
            trace = logEvent.TraceId?.ToString(),
            span = logEvent.SpanId?.ToString(),
            props = logEvent.Properties
        };
        return JsonSerializer.Serialize(obj, _options);
    }
}
