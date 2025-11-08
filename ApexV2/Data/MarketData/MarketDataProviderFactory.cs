using System;
using System.Collections.Generic;

namespace ApexV2.Data.MarketData;

/// <summary>
/// Factory for creating market data providers based on user preference
/// </summary>
public static class MarketDataProviderFactory
{
    public enum ProviderType
    {
        AlphaVantage,
        YahooFinance,
        IEX,
        Polygon,
        Finnhub
    }

    private static readonly Dictionary<ProviderType, Type> ProviderTypes = new()
    {
        { ProviderType.AlphaVantage, typeof(AlphaVantageProvider) },
        { ProviderType.YahooFinance, typeof(YahooFinanceProvider) },
        { ProviderType.IEX, typeof(IEXProvider) },
        { ProviderType.Polygon, typeof(PolygonProvider) },
        { ProviderType.Finnhub, typeof(FinnhubProvider) }
    };

    public static IMarketDataProvider CreateProvider(ProviderType providerType, string apiKey = "")
    {
        if (!ProviderTypes.ContainsKey(providerType))
        {
            throw new ArgumentException($"Provider type {providerType} is not supported");
        }

        var type = ProviderTypes[providerType];
        return (IMarketDataProvider)Activator.CreateInstance(type, apiKey)!;
    }

    public static List<ProviderInfo> GetAvailableProviders()
    {
        return new List<ProviderInfo>
        {
            new("Alpha Vantage", ProviderType.AlphaVantage, true, "Free tier: 25 requests/day, 5 per minute"),
            new("Yahoo Finance", ProviderType.YahooFinance, false, "Free, no API key required"),
            new("IEX Cloud", ProviderType.IEX, true, "Paid service with comprehensive data"),
            new("Polygon.io", ProviderType.Polygon, true, "Real-time and historical market data"),
            new("Finnhub", ProviderType.Finnhub, true, "Free tier available, professional features")
        };
    }
}

public record ProviderInfo(string Name, MarketDataProviderFactory.ProviderType Type, bool RequiresApiKey, string Description);