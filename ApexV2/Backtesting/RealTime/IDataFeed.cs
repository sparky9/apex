using System;
using System.Threading.Tasks;

namespace ApexV2.Backtesting.RealTime
{
    /// <summary>
    /// Interface for real-time market data feeds
    /// </summary>
    public interface IDataFeed
    {
        /// <summary>
        /// Event fired when new bar data arrives
        /// </summary>
        event EventHandler<BarDataEventArgs> OnBarReceived;

        /// <summary>
        /// Event fired when connection status changes
        /// </summary>
        event EventHandler<ConnectionStatusEventArgs> OnConnectionStatusChanged;

        /// <summary>
        /// Connect to the data feed
        /// </summary>
        Task<bool> ConnectAsync();

        /// <summary>
        /// Disconnect from the data feed
        /// </summary>
        Task DisconnectAsync();

        /// <summary>
        /// Subscribe to a symbol
        /// </summary>
        Task<bool> SubscribeAsync(string symbol, TimeFrame timeFrame);

        /// <summary>
        /// Unsubscribe from a symbol
        /// </summary>
        Task<bool> UnsubscribeAsync(string symbol);

        /// <summary>
        /// Check if connected
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Data feed name
        /// </summary>
        string Name { get; }
    }

    /// <summary>
    /// Event args for bar data
    /// </summary>
    public class BarDataEventArgs : EventArgs
    {
        public string Symbol { get; set; }
        public DateTime Timestamp { get; set; }
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
        public double Volume { get; set; }
        public TimeFrame TimeFrame { get; set; }
    }

    /// <summary>
    /// Connection status event args
    /// </summary>
    public class ConnectionStatusEventArgs : EventArgs
    {
        public bool IsConnected { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Time frame for bar data
    /// </summary>
    public enum TimeFrame
    {
        Tick,
        Second_1,
        Second_5,
        Second_15,
        Second_30,
        Minute_1,
        Minute_5,
        Minute_15,
        Minute_30,
        Hour_1,
        Hour_4,
        Day_1
    }
}
