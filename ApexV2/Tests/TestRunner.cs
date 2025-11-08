using System;
using System.Threading.Tasks;
using ApexV2.Tests.Automation;

namespace ApexV2.Tests
{
    /// <summary>
    /// Test runner to run automated tests and identify all issues
    /// </summary>
    public class TestRunner
    {
        public static async Task Run(string[] args)
        {
            Console.WriteLine("🚀 APEX V2 Automated Testing Suite");
            Console.WriteLine("==================================");
            Console.WriteLine("This will automatically test the entire application and identify all issues.\n");

            try
            {
                // Get project path (assuming we're in Tests folder)
                var projectPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                    System.IO.Directory.GetCurrentDirectory(), "..", "..", ".."));

                Console.WriteLine($"📁 Project Path: {projectPath}\n");

                // Create and run automated test runner
                var testRunner = new AutomatedTestRunner(projectPath);
                var summary = await testRunner.RunAllTestsAsync();

                // Display final results
                Console.WriteLine("\n" + new string('=', 50));
                Console.WriteLine("🏁 AUTOMATED TESTING COMPLETE");
                Console.WriteLine(new string('=', 50));

                var totalPassed = summary.DatabaseTestsPassed + summary.ServiceTestsPassed + 
                                summary.UITestsPassed + summary.IntegrationTestsPassed + 
                                summary.PerformanceTestsPassed;
                
                var totalTests = summary.DatabaseTestsTotal + summary.ServiceTestsTotal + 
                               summary.UITestsTotal + summary.IntegrationTestsTotal + 
                               summary.PerformanceTestsTotal;

                if (summary.CompilationPassed && totalPassed == totalTests)
                {
                    Console.WriteLine("✅ ALL TESTS PASSED! Application is healthy.");
                }
                else
                {
                    Console.WriteLine("❌ ISSUES FOUND! Check the detailed report for fixes needed.");
                    
                    if (!summary.CompilationPassed)
                    {
                        Console.WriteLine("\n🔥 CRITICAL: Compilation failed!");
                        Console.WriteLine("Fix compilation errors first before running other tests.");
                    }
                }

                Console.WriteLine($"\n📊 Overall Results: {totalPassed}/{totalTests} tests passed");
                Console.WriteLine("📄 Detailed HTML report generated in Tests/AutomatedTestReport.html");
                
                Console.WriteLine("\nPress any key to exit...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Fatal error during testing: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine("\nPress any key to exit...");
                Console.ReadKey();
            }
        }
    }
}
