namespace ApexV2.Core.Logging;

public sealed class LogManager : IAsyncDisposable
{
    private readonly List<ILogSink> _sinks = new();
    private readonly Dictionary<string, Logger> _loggers = new();
    private readonly SemaphoreSlim _dispatchLock = new(1,1);
    private static readonly AsyncLocal<Stack<Dictionary<string, object?>>> _scopeStack = new();

    public LogLevel MinimumLevel { get; set; } = LogLevel.Info;
    public bool Enabled { get; private set; } = true;

    public Logger GetLogger(string category)
    {
        lock (_loggers)
        {
            if (!_loggers.TryGetValue(category, out var logger))
            {
                logger = new Logger(category, this);
                _loggers[category] = logger;
            }
            return logger;
        }
    }

    public void AddSink(ILogSink sink)
    {
        lock (_sinks)
        {
            _sinks.Add(sink);
        }
    }

    public bool RemoveSink(ILogSink sink)
    {
        lock (_sinks)
        {
            return _sinks.Remove(sink);
        }
    }

    public void UpdateConfiguration(bool enabled, LogLevel minimumLevel)
    {
        Enabled = enabled;
        MinimumLevel = minimumLevel;
    }

    internal IReadOnlyDictionary<string, object?> GetScopeProperties()
    {
        if (_scopeStack.Value == null || _scopeStack.Value.Count == 0) return Array.Empty<KeyValuePair<string, object?>>().ToDictionary(k=>k.Key,v=> (object?)v.Value);
        // Merge bottom to top (outer to inner)
        var merged = new Dictionary<string, object?>();
        foreach (var dict in _scopeStack.Value.Reverse())
            foreach (var kv in dict)
                if (!merged.ContainsKey(kv.Key)) merged[kv.Key] = kv.Value;
        return merged;
    }

    public void UpdateFromSettings(bool enableLogging, string logLevel)
    {
        Enabled = enableLogging;
        if (Enum.TryParse<LogLevel>(logLevel, true, out var lvl)) MinimumLevel = lvl;        
    }

    public IDisposable BeginScope(string name, params (string key, object? value)[] properties)
    {
        var dict = properties.ToDictionary(p => p.key, p => p.value);
        dict["scope"] = name;
        if (_scopeStack.Value == null) _scopeStack.Value = new Stack<Dictionary<string, object?>>();
        _scopeStack.Value.Push(dict);
        return new LogScope(dict);
    }

    internal async Task DispatchAsync(LogEvent logEvent)
    {
        if (!Enabled) return;

        List<ILogSink> sinksCopy;
        lock (_sinks) sinksCopy = _sinks.ToList();
        foreach (var sink in sinksCopy)
        {
            try { await sink.WriteAsync(logEvent); }
            catch { /* swallow to avoid logging recursion */ }
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var sink in _sinks.OfType<IAsyncDisposable>())
        {
            try { await sink.DisposeAsync(); } catch { }
        }
        _dispatchLock.Dispose();
    }

    public T? GetSink<T>() where T : class, ILogSink
    {
        lock (_sinks)
        {
            return _sinks.OfType<T>().FirstOrDefault();
        }
    }

    private sealed class LogScope : IDisposable
    {
        private readonly Dictionary<string, object?> _dict;
        private bool _disposed;
        public LogScope(Dictionary<string, object?> dict) { _dict = dict; }
        public void Dispose()
        {
            if (_disposed) return;
            var stack = _scopeStack.Value;
            if (stack != null && stack.Count > 0 && ReferenceEquals(stack.Peek(), _dict))
                stack.Pop();
            _disposed = true;
        }
    }
}
