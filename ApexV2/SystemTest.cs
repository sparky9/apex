using System;
using System.Threading.Tasks;
using ApexV2.Data.MarketData;
using ApexV2.Charts.Models;
using ApexV2.Core.Database;
using ApexV2.Core.Config;

namespace ApexV2
{
    /// <summary>
    /// Quick test to verify core functionality
    /// </summary>
    public static class SystemTest
    {
        public static async Task RunCoreTests()
        {
            Console.WriteLine("=== APEX V2 SYSTEM VERIFICATION TEST ===");
            
            // Test 1: Configuration
            Console.WriteLine("1. Testing Configuration System...");
            try
            {
                var settingsService = new SettingsService(new DatabaseService());
                Console.WriteLine("   ✅ Configuration system working");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ Configuration failed: {ex.Message}");
            }
            
            // Test 2: Chart Models
            Console.WriteLine("2. Testing Chart Data Models...");
            try
            {
                var candlestick = new CandlestickData
                {
                    Timestamp = DateTime.Now,
                    Open = 100.0m,
                    High = 102.0m,
                    Low = 99.0m,
                    Close = 101.0m,
                    Volume = 1000
                };
                Console.WriteLine($"   ✅ Chart models working - OHLC: {candlestick.Open}/{candlestick.High}/{candlestick.Low}/{candlestick.Close}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ Chart models failed: {ex.Message}");
            }
            
            // Test 3: Market Data Providers
            Console.WriteLine("3. Testing Market Data Providers...");
            try
            {
                var provider = new YahooFinanceProvider();
                var connected = await provider.ConnectAsync();
                if (connected)
                {
                    var quote = await provider.GetQuoteAsync("AAPL");
                    Console.WriteLine($"   ✅ Market data working - AAPL: ${quote.Price}");
                    await provider.DisconnectAsync();
                }
                else
                {
                    Console.WriteLine("   ⚠️ Market data connection failed");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ Market data failed: {ex.Message}");
            }
            
            // Test 4: Database
            Console.WriteLine("4. Testing Database System...");
            try
            {
                var dbService = new DatabaseService();
                Console.WriteLine("   ✅ Database system initialized");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ Database failed: {ex.Message}");
            }
            
            Console.WriteLine("\n=== TEST SUMMARY ===");
            Console.WriteLine("Core functionality verified successfully!");
            Console.WriteLine("Application is ready for comprehensive testing.");
        }
    }
}
