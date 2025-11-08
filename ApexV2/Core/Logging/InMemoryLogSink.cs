using System.Collections.Concurrent;

namespace ApexV2.Core.Logging;

public sealed class InMemoryLogSink : ILogSink
{
    private readonly ConcurrentQueue<LogEvent> _events = new();
    private readonly int _capacity;

    public InMemoryLogSink(int capacity = 1000)
    {
        _capacity = capacity;
    }

    public IReadOnlyCollection<LogEvent> Events => _events.ToList().AsReadOnly();

    public ValueTask WriteAsync(LogEvent logEvent, CancellationToken ct = default)
    {
        _events.Enqueue(logEvent);
        while (_events.Count > _capacity && _events.TryDequeue(out _)) { }
        return ValueTask.CompletedTask;
    }
}
