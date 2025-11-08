using System;
using System.Collections.Generic;

namespace ApexV2.Data.Trading;

/// <summary>
/// Factory for creating trading providers with easy paper/live switching
/// </summary>
public static class TradingProviderFactory
{
    public enum ProviderType
    {
        Alpaca,
        InteractiveBrokers,
        TDAmeritrade,
        TradeStation,
        Questrade  // Canadian broker
    }

    public static ITradingProvider CreateProvider(ProviderType providerType, string apiKey, string secretKey, bool paperTrading = true)
    {
        return providerType switch
        {
            ProviderType.Alpaca => new AlpacaTradingProvider(apiKey, secretKey, paperTrading),
            ProviderType.InteractiveBrokers => new IBKRTradingProvider(apiKey, secretKey, paperTrading),
            ProviderType.TDAmeritrade => new TDAmeritradeProvider(apiKey, secretKey, paperTrading),
            ProviderType.TradeStation => new TradeStationProvider(apiKey, secretKey, paperTrading),
            ProviderType.Questrade => new QuestradeProvider(apiKey, secretKey, paperTrading),
            _ => throw new ArgumentException($"Provider type {providerType} is not supported")
        };
    }

    public static List<TradingProviderInfo> GetAvailableProviders()
    {
        return new List<TradingProviderInfo>
        {
            new("Alpaca Markets", ProviderType.Alpaca, true, true, 
                "Best API, free paper trading, US stocks + ETFs + crypto"),
            new("Interactive Brokers", ProviderType.InteractiveBrokers, true, true, 
                "Global markets including TSX, professional platform"),
            new("TD Ameritrade", ProviderType.TDAmeritrade, true, true, 
                "US broker with PaperMoney simulator"),
            new("TradeStation", ProviderType.TradeStation, true, false, 
                "US broker with SimuTrader"),
            new("Questrade", ProviderType.Questrade, false, false, 
                "Canadian broker, TSX focused (API limited)")
        };
    }
}

public record TradingProviderInfo(
    string Name, 
    TradingProviderFactory.ProviderType Type, 
    bool SupportsPaperTrading,
    bool HasGoodAPI,
    string Description
);

/// <summary>
/// Unified settings for switching between paper and live trading
/// </summary>
public class TradingSettings
{
    public TradingProviderFactory.ProviderType ProviderType { get; set; }
    public bool PaperTradingMode { get; set; } = true;  // Start safe!
    public string ApiKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public decimal MaxPositionSize { get; set; } = 1000m;  // Safety limit
    public decimal MaxDailyLoss { get; set; } = 500m;     // Safety limit
    public bool RequireConfirmation { get; set; } = true; // Safety first!
}