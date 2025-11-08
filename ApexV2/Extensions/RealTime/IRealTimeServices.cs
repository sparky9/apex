#nullable disable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ApexV2.Extensions.RealTime
{
    /// <summary>
    /// Interface for real-time data subscription management
    /// </summary>
    public interface IRealTimeSubscriptionManager : IDisposable
    {
        Task<bool> SubscribeAsync(string symbol, RealTimeDataType dataType, Action<RealTimeUpdate> callback);
        Task<bool> UnsubscribeAsync(string symbol, RealTimeDataType dataType);
        Task<bool> UnsubscribeAllAsync(string symbol);
        Task<bool> IsSubscribedAsync(string symbol, RealTimeDataType dataType);
        Task<List<string>> GetSubscribedSymbolsAsync();
        Task<Dictionary<string, List<RealTimeDataType>>> GetAllSubscriptionsAsync();
        
        event EventHandler<RealTimeUpdate> DataUpdated;
        event EventHandler<RealTimeErrorEventArgs> ErrorOccurred;
        event EventHandler<RealTimeConnectionEventArgs> ConnectionStatusChanged;
    }

    /// <summary>
    /// Interface for real-time data streaming
    /// </summary>
    public interface IRealTimeStreamingService : IDisposable
    {
        bool IsConnected { get; }
        bool IsStreaming { get; }
        
        Task<bool> ConnectAsync();
        Task DisconnectAsync();
        Task<bool> StartStreamingAsync();
        Task StopStreamingAsync();
        
        Task<bool> AddSymbolAsync(string symbol, RealTimeDataType dataType);
        Task<bool> RemoveSymbolAsync(string symbol, RealTimeDataType dataType);
        
        event EventHandler<RealTimeUpdate> DataReceived;
        event EventHandler<RealTimeErrorEventArgs> StreamError;
        event EventHandler<bool> ConnectionChanged;
    }

    /// <summary>
    /// Interface for real-time data broadcasting
    /// </summary>
    public interface IRealTimeBroadcaster : IDisposable
    {
        Task BroadcastUpdateAsync(RealTimeUpdate update);
        Task BroadcastErrorAsync(RealTimeErrorEventArgs error);
        Task BroadcastConnectionStatusAsync(bool isConnected, string provider);
        
        Task RegisterListenerAsync(string listenerId, Action<RealTimeUpdate> callback);
        Task UnregisterListenerAsync(string listenerId);
        Task<List<string>> GetActiveListenersAsync();
    }

    /// <summary>
    /// Interface for real-time data aggregation
    /// </summary>
    public interface IRealTimeAggregator : IDisposable
    {
        Task ProcessUpdateAsync(RealTimeUpdate update);
        Task<RealTimeAggregateData> GetAggregateDataAsync(string symbol, TimeSpan interval);
        Task<List<RealTimeAggregateData>> GetAggregateDataRangeAsync(string symbol, DateTime startTime, DateTime endTime, TimeSpan interval);
        
        event EventHandler<RealTimeAggregateData> AggregateUpdated;
    }

    /// <summary>
    /// Interface for real-time alerts
    /// </summary>
    public interface IRealTimeAlertService : IDisposable
    {
        Task<Guid> CreateAlertAsync(RealTimeAlert alert);
        Task<bool> UpdateAlertAsync(Guid alertId, RealTimeAlert alert);
        Task<bool> DeleteAlertAsync(Guid alertId);
        Task<RealTimeAlert> GetAlertAsync(Guid alertId);
        Task<List<RealTimeAlert>> GetAlertsForSymbolAsync(string symbol);
        Task<List<RealTimeAlert>> GetAllActiveAlertsAsync();
        
        Task ProcessUpdateForAlertsAsync(RealTimeUpdate update);
        
        event EventHandler<RealTimeAlertTriggeredEventArgs> AlertTriggered;
    }

    /// <summary>
    /// Interface for real-time metrics and monitoring
    /// </summary>
    public interface IRealTimeMetricsService : IDisposable
    {
        RealTimeMetrics GetCurrentMetrics();
        Task<RealTimeMetrics> GetMetricsSnapshotAsync();
        Task ResetMetricsAsync();
        
        Task RecordUpdateAsync(string symbol, RealTimeDataType dataType, TimeSpan latency);
        Task RecordErrorAsync(string symbol, RealTimeDataType dataType, string error);
        Task RecordConnectionEventAsync(bool connected, string provider);
        
        event EventHandler<RealTimeMetrics> MetricsUpdated;
    }
}
