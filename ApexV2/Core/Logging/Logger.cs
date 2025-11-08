using System.Diagnostics;

namespace ApexV2.Core.Logging;

public sealed class Logger
{
    private readonly string _category;
    private readonly LogManager _manager;

    internal Logger(string category, LogManager manager)
    {
        _category = category;
        _manager = manager;
    }

    private void LogInternal(LogLevel level, string message, Exception? ex = null, Dictionary<string, object?>? props = null)
    {
        if (level < _manager.MinimumLevel || !_manager.Enabled) return;
        var currentActivity = Activity.Current;
        var mergedProps = new Dictionary<string, object?>();
        foreach (var scopeKv in _manager.GetScopeProperties())
            mergedProps[scopeKv.Key] = scopeKv.Value;
        if (props != null)
            foreach (var kv in props)
                mergedProps[kv.Key] = kv.Value;
        var evt = new LogEvent
        {
            Level = level,
            Category = _category,
            Message = message,
            Exception = ex,
            TraceId = currentActivity?.TraceId,
            SpanId = currentActivity?.SpanId,
            Properties = mergedProps
        };
        _ = _manager.DispatchAsync(evt);
    }

    public void Trace(string msg, Dictionary<string, object?>? props = null) => LogInternal(LogLevel.Trace, msg, null, props);
    public void Debug(string msg, Dictionary<string, object?>? props = null) => LogInternal(LogLevel.Debug, msg, null, props);
    public void Info(string msg, Dictionary<string, object?>? props = null)  => LogInternal(LogLevel.Info, msg, null, props);
    public void Warn(string msg, Dictionary<string, object?>? props = null)  => LogInternal(LogLevel.Warn, msg, null, props);
    public void Error(string msg, Exception? ex = null, Dictionary<string, object?>? props = null) => LogInternal(LogLevel.Error, msg, ex, props);
    public void Critical(string msg, Exception? ex = null, Dictionary<string, object?>? props = null) => LogInternal(LogLevel.Critical, msg, ex, props);
}
