using System.Collections.Concurrent;
using System.IO;
using System.Text;

namespace ApexV2.Core.Logging;

public sealed class FileLogSink : ILogSink, IAsyncDisposable
{
    private readonly string _filePath;
    private readonly ILogFormatter _formatter;
    private readonly BlockingCollection<LogEvent> _queue = new(new ConcurrentQueue<LogEvent>());
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _worker;

    public FileLogSink(string filePath, ILogFormatter? formatter = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        _filePath = filePath;
        _formatter = formatter ?? new PlainTextLogFormatter();
        _worker = Task.Run(WorkerAsync);
    }

    private async Task WorkerAsync()
    {
        try
        {
            await using var stream = new FileStream(_filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            await using var writer = new StreamWriter(stream, Encoding.UTF8);
            foreach (var log in _queue.GetConsumingEnumerable(_cts.Token))
            {
                var line = _formatter.Format(log);
                await writer.WriteLineAsync(line);
                await writer.FlushAsync();
            }
        }
        catch (OperationCanceledException) { }
    }

    public ValueTask WriteAsync(LogEvent logEvent, CancellationToken ct = default)
    {
        if (!_queue.IsAddingCompleted)
        {
            _queue.Add(logEvent, ct);
        }
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _queue.CompleteAdding();
        _cts.Cancel();
        try { await _worker; } catch { }
        _cts.Dispose();
        _queue.Dispose();
    }
}
