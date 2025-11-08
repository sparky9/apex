#nullable disable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ApexV2.Extensions.API
{
    // Core API interfaces
    public interface IApiKeyService
    {
        Task<ApiKeyResponse> CreateApiKeyAsync(ApiKeyRequest request);
        Task<ApiKeyResponse> GetApiKeyAsync(string keyId);
        Task<List<ApiKeyResponse>> GetApiKeysAsync();
        Task<bool> ValidateApiKeyAsync(string apiKey);
        Task<bool> RevokeApiKeyAsync(string keyId);
        Task<ApiKeyResponse> UpdateApiKeyAsync(string keyId, ApiKeyRequest request);
        Task<List<string>> GetPermissionsAsync(string apiKey);
    }

    public interface IApiRateLimitService
    {
        Task<bool> CheckRateLimitAsync(string apiKey, string endpoint);
        Task<RateLimitInfo> GetRateLimitInfoAsync(string apiKey);
        Task IncrementRequestCountAsync(string apiKey, string endpoint);
        Task ResetRateLimitAsync(string apiKey);
    }

    public interface IApiAuthenticationService
    {
        Task<bool> AuthenticateAsync(string apiKey);
        Task<bool> AuthorizeAsync(string apiKey, string permission);
        Task<string> GetUserIdAsync(string apiKey);
        Task LogApiAccessAsync(string apiKey, string endpoint, bool success);
    }

    // Market Data API interface
    public interface IMarketDataApiService
    {
        Task<ApiResponse<MarketDataResponse>> GetMarketDataAsync(MarketDataRequest request);
        Task<ApiResponse<List<string>>> GetAvailableSymbolsAsync(string market = null);
        Task<ApiResponse<List<string>>> GetAvailableTimeframesAsync();
        Task<ApiResponse<CandleData>> GetLatestQuoteAsync(string symbol);
        Task<ApiResponse<List<CandleData>>> GetIntradayDataAsync(string symbol, string timeframe);
        Task<ApiResponse<Dictionary<string, CandleData>>> GetMultipleQuotesAsync(List<string> symbols);
    }

    // Watchlist API interface
    public interface IWatchlistApiService
    {
        Task<ApiResponse<WatchlistResponse>> CreateWatchlistAsync(WatchlistRequest request);
        Task<ApiResponse<WatchlistResponse>> GetWatchlistAsync(string watchlistId);
        Task<ApiResponse<List<WatchlistResponse>>> GetWatchlistsAsync();
        Task<ApiResponse<WatchlistResponse>> UpdateWatchlistAsync(string watchlistId, WatchlistRequest request);
        Task<ApiResponse<bool>> DeleteWatchlistAsync(string watchlistId);
        Task<ApiResponse<bool>> AddSymbolToWatchlistAsync(string watchlistId, string symbol);
        Task<ApiResponse<bool>> RemoveSymbolFromWatchlistAsync(string watchlistId, string symbol);
        Task<ApiResponse<List<WatchlistItem>>> GetWatchlistQuotesAsync(string watchlistId);
    }

    // Scanner API interface
    public interface IScannerApiService
    {
        Task<ApiResponse<ScanResponse>> ExecuteScanAsync(ScanRequest request);
        Task<ApiResponse<ScanResponse>> GetScanResultsAsync(string scanId);
        Task<ApiResponse<List<string>>> GetAvailableCriteriaAsync();
        Task<ApiResponse<List<string>>> GetAvailableMarketsAsync();
        Task<ApiResponse<ScanResponse>> SaveScanAsync(string scanId, string name);
        Task<ApiResponse<List<ScanResponse>>> GetSavedScansAsync();
    }

    // Indicator API interface
    public interface IIndicatorApiService
    {
        Task<ApiResponse<IndicatorResponse>> CalculateIndicatorAsync(IndicatorRequest request);
        Task<ApiResponse<List<string>>> GetAvailableIndicatorsAsync();
        Task<ApiResponse<Dictionary<string, object>>> GetIndicatorParametersAsync(string indicatorType);
        Task<ApiResponse<Dictionary<string, IndicatorResponse>>> CalculateMultipleIndicatorsAsync(
            string symbol, List<IndicatorRequest> requests);
    }

    // Portfolio API interface
    public interface IPortfolioApiService
    {
        Task<ApiResponse<PortfolioResponse>> GetPortfolioAsync(PortfolioRequest request);
        Task<ApiResponse<List<PortfolioResponse>>> GetPortfoliosAsync();
        Task<ApiResponse<PerformanceData>> GetPerformanceAsync(string portfolioId, DateTime? startDate = null);
        Task<ApiResponse<List<PositionData>>> GetPositionsAsync(string portfolioId);
        Task<ApiResponse<PositionData>> GetPositionAsync(string portfolioId, string symbol);
    }

    // Webhook API interface
    public interface IWebhookApiService
    {
        Task<ApiResponse<WebhookResponse>> CreateWebhookAsync(WebhookRequest request);
        Task<ApiResponse<WebhookResponse>> GetWebhookAsync(string webhookId);
        Task<ApiResponse<List<WebhookResponse>>> GetWebhooksAsync();
        Task<ApiResponse<WebhookResponse>> UpdateWebhookAsync(string webhookId, WebhookRequest request);
        Task<ApiResponse<bool>> DeleteWebhookAsync(string webhookId);
        Task<ApiResponse<bool>> TestWebhookAsync(string webhookId);
        Task<ApiResponse<List<string>>> GetAvailableEventsAsync();
    }

    // Data Export API interface
    public interface IDataExportApiService
    {
        Task<ApiResponse<byte[]>> ExportMarketDataAsync(string symbol, string format, DateTime? startDate = null, DateTime? endDate = null);
        Task<ApiResponse<byte[]>> ExportWatchlistAsync(string watchlistId, string format);
        Task<ApiResponse<byte[]>> ExportScanResultsAsync(string scanId, string format);
        Task<ApiResponse<byte[]>> ExportPortfolioAsync(string portfolioId, string format);
        Task<ApiResponse<List<string>>> GetSupportedFormatsAsync();
    }

    // Fundamental Data API interface
    public interface IFundamentalDataApiService
    {
        Task<ApiResponse<Dictionary<string, object>>> GetFundamentalsAsync(string symbol);
        Task<ApiResponse<Dictionary<string, object>>> GetFinancialRatiosAsync(string symbol);
        Task<ApiResponse<Dictionary<string, object>>> GetCompanyInfoAsync(string symbol);
        Task<ApiResponse<Dictionary<string, object>>> GetEarningsAsync(string symbol);
        Task<ApiResponse<Dictionary<string, object>>> GetDividendsAsync(string symbol);
    }

    // News API interface
    public interface INewsApiService
    {
        Task<ApiResponse<List<object>>> GetNewsAsync(string symbol = null, int limit = 20);
        Task<ApiResponse<List<object>>> GetMarketNewsAsync(int limit = 20);
        Task<ApiResponse<List<object>>> GetEarningsNewsAsync(string symbol = null);
        Task<ApiResponse<object>> GetNewsArticleAsync(string articleId);
    }

    // Alert API interface
    public interface IAlertApiService
    {
        Task<ApiResponse<object>> CreateAlertAsync(object alertRequest);
        Task<ApiResponse<object>> GetAlertAsync(string alertId);
        Task<ApiResponse<List<object>>> GetAlertsAsync();
        Task<ApiResponse<object>> UpdateAlertAsync(string alertId, object alertRequest);
        Task<ApiResponse<bool>> DeleteAlertAsync(string alertId);
        Task<ApiResponse<bool>> TriggerAlertAsync(string alertId);
    }

    // System Info API interface
    public interface ISystemInfoApiService
    {
        Task<ApiResponse<object>> GetSystemStatusAsync();
        Task<ApiResponse<object>> GetApiVersionAsync();
        Task<ApiResponse<object>> GetServerTimeAsync();
        Task<ApiResponse<object>> GetMarketStatusAsync();
        Task<ApiResponse<object>> GetUsageStatsAsync(string apiKey);
    }
}
