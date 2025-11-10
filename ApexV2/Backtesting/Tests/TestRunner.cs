using System;

namespace ApexV2.Backtesting.Tests
{
    /// <summary>
    /// Simple test runner for backtesting validation
    /// </summary>
    public class TestRunner
    {
        public static void Main(string[] args)
        {
            try
            {
                Console.Clear();
                Console.WriteLine();
                Console.WriteLine("🚀 APEX V3 BACKTESTING ENGINE - VALIDATION TEST 🚀");
                Console.WriteLine();
                Console.WriteLine("Testing complete backtesting pipeline:");
                Console.WriteLine("  • Sample data generation");
                Console.WriteLine("  • Technical indicator calculation (RSI, ATR, SMA)");
                Console.WriteLine("  • Strategy signal generation");
                Console.WriteLine("  • Trade execution simulation");
                Console.WriteLine("  • Performance analytics");
                Console.WriteLine();
                Console.WriteLine("Press any key to start...");
                Console.ReadKey();
                Console.Clear();

                // Run the test
                SimpleStrategyTest.RunTest();

                Console.WriteLine();
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("❌ ERROR OCCURRED:");
                Console.WriteLine($"   {ex.Message}");
                Console.WriteLine();
                Console.WriteLine("Stack trace:");
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine();
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
        }
    }
}
