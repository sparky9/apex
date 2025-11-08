using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using ApexV2.Core.Database;
using ApexV2.Core.Config;
using ApexV2.Data.MarketData;
using ApexV2.Dashboard.Portfolio;
using ApexV2.Analysis.Alerts;
using ApexV2.Charts.Models;
using System.IO;
using System.Diagnostics;

namespace ApexV2.Tests.Integration
{
    /// <summary>
    /// Comprehensive integration tests for the entire APEX V2 application
    /// Tests core functionality, data flow, and component integration
    /// </summary>
    public class ApplicationIntegrationTests : IDisposable
    {
        private readonly string _testDatabasePath;
        private readonly IServiceProvider _serviceProvider;
        private DatabaseService _databaseService;
        private SettingsService _settingsService;

        public ApplicationIntegrationTests()
        {
            // Create test database in temp directory
            _testDatabasePath = Path.Combine(Path.GetTempPath(), $"apex_test_{Guid.NewGuid()}.db");
            
            // Setup test service provider
            var services = new ServiceCollection();
            ConfigureTestServices(services);
            _serviceProvider = services.BuildServiceProvider();
            
            _databaseService = _serviceProvider.GetRequiredService<DatabaseService>();
            _settingsService = _serviceProvider.GetRequiredService<SettingsService>();
        }

        private void ConfigureTestServices(IServiceCollection services)
        {
            // Database services
            services.AddSingleton<DatabaseService>();
            services.AddDbContext<ApexDbContext>();
            
            // Configuration services
            services.AddSingleton<SettingsService>();
            
            // Market data services
            services.AddSingleton<IMarketDataProvider, TestMarketDataProvider>();
            
            // Portfolio services
            services.AddSingleton<PortfolioService>();
            
            // Analysis services
            services.AddSingleton<AlertManager>();
        }

        [Fact]
        public async Task Database_Initialization_Should_Succeed()
        {
            // Arrange & Act
            var result = await _databaseService.InitializeDatabaseAsync();
            
            // Assert
            Assert.True(result, "Database initialization should succeed");
            Assert.True(File.Exists(_testDatabasePath), "Database file should be created");
        }

        [Fact]
        public async Task Settings_LoadAndSave_Should_Work()
        {
            // Arrange
            await _databaseService.InitializeDatabaseAsync();
            
            // Act - Load default settings
            var settings = await _settingsService.LoadSettingsAsync();
            Assert.NotNull(settings);
            
            // Modify settings
            settings.Appearance.Theme = "Dark";
            settings.Data.DefaultProvider = "IEX";
            
            // Save settings
            await _settingsService.SaveSettingsAsync(settings);
            
            // Load again to verify persistence
            var reloadedSettings = await _settingsService.LoadSettingsAsync();
            
            // Assert
            Assert.Equal("Dark", reloadedSettings.Appearance.Theme);
            Assert.Equal("IEX", reloadedSettings.Data.DefaultProvider);
        }

        [Fact]
        public async Task MarketData_Provider_Should_Connect()
        {
            // Arrange
            var provider = _serviceProvider.GetRequiredService<IMarketDataProvider>();
            
            // Act
            var connectionResult = await provider.ConnectAsync();
            
            // Assert
            Assert.True(connectionResult, "Market data provider should connect successfully");
        }

        [Fact]
        public async Task Portfolio_Service_Should_Initialize()
        {
            // Arrange
            await _databaseService.InitializeDatabaseAsync();
            var portfolioService = _serviceProvider.GetRequiredService<PortfolioService>();
            
            // Act
            var portfolios = portfolioService.GetAllPortfolios();
            
            // Assert
            Assert.NotNull(portfolios);
            // Should have at least a default portfolio or empty list
        }

        [Fact]
        public async Task AlertManager_Should_Initialize()
        {
            // Arrange
            await _databaseService.InitializeDatabaseAsync();
            var alertManager = _serviceProvider.GetRequiredService<AlertManager>();
            
            // Act
            var summary = await alertManager.GetDashboardSummaryAsync();
            
            // Assert
            Assert.NotNull(summary);
            Assert.True(summary.TotalAlerts >= 0);
        }

        [Fact]
        public async Task Full_Application_Startup_Simulation()
        {
            // This test simulates the complete application startup sequence
            
            // Step 1: Initialize database
            var dbResult = await _databaseService.InitializeDatabaseAsync();
            Assert.True(dbResult, "Database initialization failed");
            
            // Step 2: Load settings
            var settings = await _settingsService.LoadSettingsAsync();
            Assert.NotNull(settings);
            
            // Step 3: Initialize market data
            var provider = _serviceProvider.GetRequiredService<IMarketDataProvider>();
            var connectionResult = await provider.ConnectAsync();
            Assert.True(connectionResult, "Market data connection failed");
            
            // Step 4: Initialize portfolio service
            var portfolioService = _serviceProvider.GetRequiredService<PortfolioService>();
            var portfolios = portfolioService.GetAllPortfolios();
            Assert.NotNull(portfolios);
            
            // Step 5: Initialize alert manager
            var alertManager = _serviceProvider.GetRequiredService<AlertManager>();
            var summary = await alertManager.GetDashboardSummaryAsync();
            Assert.NotNull(summary);
            
            // If we get here, the full startup sequence succeeded
            Assert.True(true, "Full application startup simulation completed successfully");
        }

        public void Dispose()
        {
            // ServiceProvider implements IDisposable, so cast and dispose
            if (_serviceProvider is IDisposable disposableProvider)
            {
                disposableProvider.Dispose();
            }
            
            // Clean up test database
            if (File.Exists(_testDatabasePath))
            {
                try
                {
                    File.Delete(_testDatabasePath);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }

    /// <summary>
    /// Test implementation of market data provider for testing
    /// </summary>
    public class TestMarketDataProvider : IMarketDataProvider
    {
        public string ProviderName => "Test Provider";
        public bool IsConnected { get; private set; }
        public event EventHandler<PriceUpdateEventArgs>? PriceUpdate;

        public async Task<bool> ConnectAsync()
        {
            await Task.Delay(100); // Simulate connection delay
            IsConnected = true;
            return true;
        }

        public async Task DisconnectAsync()
        {
            await Task.Delay(50);
            IsConnected = false;
        }

        public async Task<StockQuote> GetQuoteAsync(string symbol)
        {
            await Task.Delay(10);
            return new StockQuote
            {
                Symbol = symbol,
                Price = 100.00m,
                Volume = 1000,
                Timestamp = DateTime.Now
            };
        }

        public Task<StockQuote> GetCurrentQuoteAsync(string symbol)
        {
            return GetQuoteAsync(symbol);
        }

        public async Task<List<StockQuote>> GetQuotesAsync(List<string> symbols)
        {
            var quotes = new List<StockQuote>();
            foreach (var symbol in symbols)
            {
                quotes.Add(await GetQuoteAsync(symbol));
            }
            return quotes;
        }

        public async Task<List<HistoricalPrice>> GetHistoricalDataAsync(string symbol, DateTime startDate, DateTime endDate)
        {
            await Task.Delay(50);
            var data = new List<HistoricalPrice>();
            
            for (int i = 0; i < 10; i++)
            {
                data.Add(new HistoricalPrice
                {
                    Symbol = symbol,
                    Date = startDate.AddDays(i),
                    Open = 100 + i,
                    High = 105 + i,
                    Low = 95 + i,
                    Close = 102 + i,
                    Volume = 1000
                });
            }
            
            return data;
        }

        public async Task<List<StockQuote>> SearchSymbolsAsync(string query)
        {
            await Task.Delay(30);
            return new List<StockQuote>
            {
                new StockQuote { Symbol = $"{query}.TST", Price = 100m }
            };
        }

        public async Task<CompanyFundamentals> GetFundamentalsAsync(string symbol)
        {
            await Task.Delay(20);
            return new CompanyFundamentals { Symbol = symbol };
        }

        public async Task<List<NewsItem>> GetNewsAsync(string? symbol = null)
        {
            await Task.Delay(25);
            return new List<NewsItem>();
        }
    }
}
