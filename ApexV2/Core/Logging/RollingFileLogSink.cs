using System.Collections.Concurrent;
using System.Text;
using System.IO;

namespace ApexV2.Core.Logging;

public sealed class RollingFileLogSink : ILogSink, IAsyncDisposable
{
    private readonly string _directory;
    private readonly string _filePrefix;
    private readonly long _maxBytes;
    private readonly int _maxFiles;
    private readonly ILogFormatter _formatter;
    private readonly BlockingCollection<LogEvent> _queue = new();
    private readonly CancellationTokenSource _cts = new();
    private Task _worker;
    private FileStream? _currentStream;
    private StreamWriter? _writer;
    private string _currentPath = string.Empty;

    public RollingFileLogSink(string directory, string filePrefix = "apex", long maxBytes = 5_000_000, int maxFiles = 10, ILogFormatter? formatter = null)
    {
        _directory = directory;
        _filePrefix = filePrefix;
        _maxBytes = maxBytes;
        _maxFiles = Math.Max(1, maxFiles);
        _formatter = formatter ?? new PlainTextLogFormatter();
        Directory.CreateDirectory(_directory);
        OpenNewFile();
        _worker = Task.Run(WorkerAsync);
    }

    private void OpenNewFile()
    {
        _writer?.Dispose();
        _currentStream?.Dispose();
        _currentPath = Path.Combine(_directory, $"{_filePrefix}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.log");
        _currentStream = new FileStream(_currentPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        _writer = new StreamWriter(_currentStream, Encoding.UTF8) { AutoFlush = true };
        EnforceRetention();
    }

    private void EnforceRetention()
    {
        var files = Directory.GetFiles(_directory, $"{_filePrefix}_*.log")
                              .OrderByDescending(f => f)
                              .ToList();
        if (files.Count <= _maxFiles) return;
        foreach (var old in files.Skip(_maxFiles))
        {
            try { File.Delete(old); } catch { }
        }
    }

    private async Task WorkerAsync()
    {
        try
        {
            foreach (var log in _queue.GetConsumingEnumerable(_cts.Token))
            {
                if (_currentStream == null || _writer == null) OpenNewFile();
                if (_currentStream!.Length > _maxBytes) OpenNewFile();
                var line = _formatter.Format(log);
                await _writer!.WriteLineAsync(line);
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
        _writer?.Dispose();
        _currentStream?.Dispose();
        _queue.Dispose();
        _cts.Dispose();
    }
}
