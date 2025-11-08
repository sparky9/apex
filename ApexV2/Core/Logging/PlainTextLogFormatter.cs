using System.Text;

namespace ApexV2.Core.Logging;

public sealed class PlainTextLogFormatter : ILogFormatter
{
    public string Format(LogEvent logEvent)
    {
        var sb = new StringBuilder();
        sb.Append('[').Append(logEvent.TimestampUtc.ToString("yyyy-MM-dd HH:mm:ss.fff"))
          .Append("] [").Append(logEvent.Level)
          .Append("] [").Append(logEvent.Category).Append("] ")
          .Append(logEvent.Message);
        if (logEvent.Exception != null)
        {
            sb.Append(" | ").Append(logEvent.Exception.GetType().Name)
              .Append(':').Append(' ').Append(logEvent.Exception.Message);
        }
        return sb.ToString();
    }
}
