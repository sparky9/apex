using ApexV2.Core.Logging;

namespace ApexV2.Charts.Export;

/// <summary>
/// Logger interface for chart export services
/// </summary>
public interface IChartLogger
{
    void Trace(string msg, Dictionary<string, object?>? props = null);
    void Debug(string msg, Dictionary<string, object?>? props = null);
    void Info(string msg, Dictionary<string, object?>? props = null);
    void Warn(string msg, Dictionary<string, object?>? props = null);
    void Error(string msg, Exception? ex = null, Dictionary<string, object?>? props = null);
    void Critical(string msg, Exception? ex = null, Dictionary<string, object?>? props = null);
}

/// <summary>
/// Logger adapter to wrap the existing Logger class
/// </summary>
public class LoggerAdapter : IChartLogger
{
    private readonly Logger _logger;
    
    public LoggerAdapter(Logger logger)
    {
        _logger = logger;
    }
    
    public void Trace(string msg, Dictionary<string, object?>? props = null) => _logger.Trace(msg, props);
    public void Debug(string msg, Dictionary<string, object?>? props = null) => _logger.Debug(msg, props);
    public void Info(string msg, Dictionary<string, object?>? props = null) => _logger.Info(msg, props);
    public void Warn(string msg, Dictionary<string, object?>? props = null) => _logger.Warn(msg, props);
    public void Error(string msg, Exception? ex = null, Dictionary<string, object?>? props = null) => _logger.Error(msg, ex, props);
    public void Critical(string msg, Exception? ex = null, Dictionary<string, object?>? props = null) => _logger.Critical(msg, ex, props);
}

/// <summary>
/// Null logger implementation for testing and scenarios where logging is not needed
/// </summary>
public class NullLogger : IChartLogger
{
    public void Trace(string msg, Dictionary<string, object?>? props = null) { }
    public void Debug(string msg, Dictionary<string, object?>? props = null) { }
    public void Info(string msg, Dictionary<string, object?>? props = null) { }
    public void Warn(string msg, Dictionary<string, object?>? props = null) { }
    public void Error(string msg, Exception? ex = null, Dictionary<string, object?>? props = null) { }
    public void Critical(string msg, Exception? ex = null, Dictionary<string, object?>? props = null) { }
}
