#nullable disable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ApexV2.Extensions.API;

namespace ApexV2.Tests.Extensions.API
{
    [TestClass]
    public class ApiKeyServiceTests
    {
        private ApiKeyService _apiKeyService;

        [TestInitialize]
        public void Setup()
        {
            _apiKeyService = new ApiKeyService();
        }

        [TestMethod]
        public async Task CreateApiKeyAsync_WithValidRequest_ShouldReturnApiKey()
        {
            // Arrange
            var request = new ApiKeyRequest
            {
                Name = "Test API Key",
                Description = "Test key for unit testing",
                Permissions = new List<string> { "marketData:read", "watchlist:read" },
                ExpiresAt = DateTime.UtcNow.AddMonths(6)
            };

            // Act
            var result = await _apiKeyService.CreateApiKeyAsync(request);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(request.Name, result.Name);
            Assert.AreEqual(request.Description, result.Description);
            Assert.IsNotNull(result.Key);
            Assert.AreEqual(64, result.Key.Length); // Generated key should be 64 characters
            Assert.IsTrue(result.IsActive);
            Assert.IsNotNull(result.Id);
        }

        [TestMethod]
        public async Task ValidateApiKeyAsync_WithValidKey_ShouldReturnTrue()
        {
            // Arrange
            var request = new ApiKeyRequest
            {
                Name = "Test API Key",
                Description = "Test key",
                Permissions = new List<string> { "*" }
            };
            var apiKey = await _apiKeyService.CreateApiKeyAsync(request);

            // Act
            var isValid = await _apiKeyService.ValidateApiKeyAsync(apiKey.Key);

            // Assert
            Assert.IsTrue(isValid);
        }

        [TestMethod]
        public async Task ValidateApiKeyAsync_WithInvalidKey_ShouldReturnFalse()
        {
            // Act
            var isValid = await _apiKeyService.ValidateApiKeyAsync("invalid-key");

            // Assert
            Assert.IsFalse(isValid);
        }

        [TestMethod]
        public async Task ValidateApiKeyAsync_WithExpiredKey_ShouldReturnFalse()
        {
            // Arrange
            var request = new ApiKeyRequest
            {
                Name = "Expired Key",
                Description = "Key that expires immediately",
                Permissions = new List<string> { "*" },
                ExpiresAt = DateTime.UtcNow.AddMinutes(-1) // Expired 1 minute ago
            };
            var apiKey = await _apiKeyService.CreateApiKeyAsync(request);

            // Act
            var isValid = await _apiKeyService.ValidateApiKeyAsync(apiKey.Key);

            // Assert
            Assert.IsFalse(isValid);
        }

        [TestMethod]
        public async Task RevokeApiKeyAsync_WithValidKeyId_ShouldRevokeKey()
        {
            // Arrange
            var request = new ApiKeyRequest
            {
                Name = "Key to Revoke",
                Description = "This key will be revoked",
                Permissions = new List<string> { "*" }
            };
            var apiKey = await _apiKeyService.CreateApiKeyAsync(request);

            // Act
            var revoked = await _apiKeyService.RevokeApiKeyAsync(apiKey.Id);

            // Assert
            Assert.IsTrue(revoked);

            // Verify key is no longer valid
            var isValid = await _apiKeyService.ValidateApiKeyAsync(apiKey.Key);
            Assert.IsFalse(isValid);
        }

        [TestMethod]
        public async Task GetApiKeysAsync_ShouldReturnAllKeys()
        {
            // Arrange
            var request1 = new ApiKeyRequest { Name = "Key 1", Permissions = new List<string> { "*" } };
            var request2 = new ApiKeyRequest { Name = "Key 2", Permissions = new List<string> { "*" } };
            
            await _apiKeyService.CreateApiKeyAsync(request1);
            await _apiKeyService.CreateApiKeyAsync(request2);

            // Act
            var keys = await _apiKeyService.GetApiKeysAsync();

            // Assert
            Assert.IsNotNull(keys);
            Assert.AreEqual(2, keys.Count);
        }

        [TestMethod]
        public async Task GetPermissionsAsync_WithValidKey_ShouldReturnPermissions()
        {
            // Arrange
            var permissions = new List<string> { "marketData:read", "watchlist:write" };
            var request = new ApiKeyRequest
            {
                Name = "Permissions Test",
                Permissions = permissions
            };
            var apiKey = await _apiKeyService.CreateApiKeyAsync(request);

            // Act
            var result = await _apiKeyService.GetPermissionsAsync(apiKey.Key);

            // Assert
            Assert.IsNotNull(result);
            CollectionAssert.AreEqual(permissions, result);
        }
    }

    [TestClass]
    public class ApiRateLimitServiceTests
    {
        private ApiRateLimitService _rateLimitService;

        [TestInitialize]
        public void Setup()
        {
            _rateLimitService = new ApiRateLimitService();
        }

        [TestMethod]
        public async Task CheckRateLimitAsync_WithNewApiKey_ShouldAllowRequest()
        {
            // Arrange
            var apiKey = "test-api-key";
            var endpoint = "/api/marketdata";

            // Act
            var allowed = await _rateLimitService.CheckRateLimitAsync(apiKey, endpoint);

            // Assert
            Assert.IsTrue(allowed);
        }

        [TestMethod]
        public async Task GetRateLimitInfoAsync_WithNewApiKey_ShouldReturnDefaultLimits()
        {
            // Arrange
            var apiKey = "test-api-key";

            // Act
            var info = await _rateLimitService.GetRateLimitInfoAsync(apiKey);

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual(100, info.RequestsPerMinute);
            Assert.AreEqual(5000, info.RequestsPerHour);
            Assert.AreEqual(50000, info.RequestsPerDay);
            Assert.AreEqual(100, info.RemainingRequests);
        }

        [TestMethod]
        public async Task IncrementRequestCountAsync_ShouldDecrementRemainingRequests()
        {
            // Arrange
            var apiKey = "test-api-key";
            var endpoint = "/api/marketdata";

            // Get initial count
            var initialInfo = await _rateLimitService.GetRateLimitInfoAsync(apiKey);
            var initialRemaining = initialInfo.RemainingRequests;

            // Act
            await _rateLimitService.IncrementRequestCountAsync(apiKey, endpoint);

            // Assert
            var updatedInfo = await _rateLimitService.GetRateLimitInfoAsync(apiKey);
            Assert.AreEqual(initialRemaining - 1, updatedInfo.RemainingRequests);
        }

        [TestMethod]
        public async Task ResetRateLimitAsync_ShouldResetToDefault()
        {
            // Arrange
            var apiKey = "test-api-key";
            var endpoint = "/api/marketdata";

            // Use some requests
            await _rateLimitService.IncrementRequestCountAsync(apiKey, endpoint);
            await _rateLimitService.IncrementRequestCountAsync(apiKey, endpoint);

            // Act
            await _rateLimitService.ResetRateLimitAsync(apiKey);

            // Assert
            var info = await _rateLimitService.GetRateLimitInfoAsync(apiKey);
            Assert.AreEqual(100, info.RemainingRequests);
        }
    }

    [TestClass]
    public class MarketDataApiServiceTests
    {
        private MarketDataApiService _marketDataService;

        [TestInitialize]
        public void Setup()
        {
            _marketDataService = new MarketDataApiService();
        }

        [TestMethod]
        public async Task GetMarketDataAsync_WithValidSymbol_ShouldReturnData()
        {
            // Arrange
            var request = new MarketDataRequest
            {
                Symbol = "AAPL",
                Timeframe = "1d",
                Limit = 10
            };

            // Act
            var response = await _marketDataService.GetMarketDataAsync(request);

            // Assert
            Assert.IsNotNull(response);
            Assert.IsTrue(response.Success);
            Assert.IsNotNull(response.Data);
            Assert.AreEqual("AAPL", response.Data.Symbol);
            Assert.IsTrue(response.Data.Candles.Count <= 10);
            Assert.IsNotNull(response.Data.Metadata);
        }

        [TestMethod]
        public async Task GetAvailableSymbolsAsync_ShouldReturnSymbolList()
        {
            // Act
            var response = await _marketDataService.GetAvailableSymbolsAsync();

            // Assert
            Assert.IsNotNull(response);
            Assert.IsTrue(response.Success);
            Assert.IsNotNull(response.Data);
            Assert.IsTrue(response.Data.Count > 0);
        }

        [TestMethod]
        public async Task GetAvailableSymbolsAsync_WithTsxMarket_ShouldReturnTsxSymbols()
        {
            // Act
            var response = await _marketDataService.GetAvailableSymbolsAsync("TSX");

            // Assert
            Assert.IsNotNull(response);
            Assert.IsTrue(response.Success);
            Assert.IsNotNull(response.Data);
            Assert.IsTrue(response.Data.All(s => s.EndsWith(".TO")));
        }

        [TestMethod]
        public async Task GetLatestQuoteAsync_WithValidSymbol_ShouldReturnQuote()
        {
            // Arrange
            var symbol = "AAPL";

            // Act
            var response = await _marketDataService.GetLatestQuoteAsync(symbol);

            // Assert
            Assert.IsNotNull(response);
            Assert.IsTrue(response.Success);
            Assert.IsNotNull(response.Data);
            Assert.IsTrue(response.Data.Open > 0);
            Assert.IsTrue(response.Data.High > 0);
            Assert.IsTrue(response.Data.Low > 0);
            Assert.IsTrue(response.Data.Close > 0);
            Assert.IsTrue(response.Data.Volume > 0);
        }

        [TestMethod]
        public async Task GetMultipleQuotesAsync_WithValidSymbols_ShouldReturnAllQuotes()
        {
            // Arrange
            var symbols = new List<string> { "AAPL", "MSFT", "GOOGL" };

            // Act
            var response = await _marketDataService.GetMultipleQuotesAsync(symbols);

            // Assert
            Assert.IsNotNull(response);
            Assert.IsTrue(response.Success);
            Assert.IsNotNull(response.Data);
            Assert.AreEqual(symbols.Count, response.Data.Count);
            
            foreach (var symbol in symbols)
            {
                Assert.IsTrue(response.Data.ContainsKey(symbol));
                Assert.IsNotNull(response.Data[symbol]);
            }
        }
    }

    [TestClass]
    public class ApiManagerTests
    {
        private ApiManager _apiManager;

        [TestInitialize]
        public void Setup()
        {
            _apiManager = new ApiManager();
        }

        [TestMethod]
        public async Task InitializeAsync_ShouldInitializeSuccessfully()
        {
            // Act
            await _apiManager.InitializeAsync();

            // Assert
            var status = _apiManager.GetApiStatus();
            Assert.IsTrue((bool)status["Initialized"]);
            Assert.IsTrue((int)status["ServicesCount"] > 0);
        }

        [TestMethod]
        public async Task GetApiService_WithValidType_ShouldReturnService()
        {
            // Arrange
            await _apiManager.InitializeAsync();

            // Act
            var marketDataService = _apiManager.GetApiService<IMarketDataApiService>();

            // Assert
            Assert.IsNotNull(marketDataService);
        }

        [TestMethod]
        public async Task ValidateApiKeyAsync_WithValidKey_ShouldReturnTrue()
        {
            // Arrange
            await _apiManager.InitializeAsync();
            var apiKeyService = _apiManager.GetApiKeyService();
            var request = new ApiKeyRequest
            {
                Name = "Test Key",
                Permissions = new List<string> { "*" }
            };
            var apiKey = await apiKeyService.CreateApiKeyAsync(request);

            // Act
            var isValid = await _apiManager.ValidateApiKeyAsync(apiKey.Key);

            // Assert
            Assert.IsTrue(isValid);
        }

        [TestMethod]
        public async Task AuthorizeRequestAsync_WithValidPermission_ShouldReturnTrue()
        {
            // Arrange
            await _apiManager.InitializeAsync();
            var apiKeyService = _apiManager.GetApiKeyService();
            var request = new ApiKeyRequest
            {
                Name = "Test Key",
                Permissions = new List<string> { "marketData:read" }
            };
            var apiKey = await apiKeyService.CreateApiKeyAsync(request);

            // Act
            var authorized = await _apiManager.AuthorizeRequestAsync(apiKey.Key, "marketData:read");

            // Assert
            Assert.IsTrue(authorized);
        }

        [TestMethod]
        public async Task CheckRateLimitAsync_WithValidKey_ShouldReturnTrue()
        {
            // Arrange
            await _apiManager.InitializeAsync();
            var apiKeyService = _apiManager.GetApiKeyService();
            var request = new ApiKeyRequest
            {
                Name = "Test Key",
                Permissions = new List<string> { "*" }
            };
            var apiKey = await apiKeyService.CreateApiKeyAsync(request);

            // Act
            var allowed = await _apiManager.CheckRateLimitAsync(apiKey.Key, "/api/test");

            // Assert
            Assert.IsTrue(allowed);
        }

        [TestMethod]
        public void GetApiStatus_ShouldReturnValidStatus()
        {
            // Act
            var status = _apiManager.GetApiStatus();

            // Assert
            Assert.IsNotNull(status);
            Assert.IsTrue(status.ContainsKey("Initialized"));
            Assert.IsTrue(status.ContainsKey("ServicesCount"));
            Assert.IsTrue(status.ContainsKey("ApiKeysCount"));
            Assert.IsTrue(status.ContainsKey("Version"));
            Assert.IsTrue(status.ContainsKey("Timestamp"));
        }

        [TestMethod]
        public async Task ShutdownAsync_ShouldShutdownCleanly()
        {
            // Arrange
            await _apiManager.InitializeAsync();

            // Act
            await _apiManager.ShutdownAsync();

            // Assert
            var status = _apiManager.GetApiStatus();
            Assert.IsFalse((bool)status["Initialized"]);
        }
    }
}
