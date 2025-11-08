using System;
using System.Threading.Tasks;
using ApexV2.Validation;

namespace ApexV2.Validation
{
    public class ValidationRunner
    {
        public static async Task Run(string[] args)
        {
            Console.WriteLine("🚀 APEX V2 Automated Validation Suite");
            Console.WriteLine("=====================================");
            Console.WriteLine("This will automatically identify and report ALL issues in your application.\n");

            try
            {
                // Get project path
            var projectPath = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), ".."));

            Console.WriteLine($"📁 Analyzing project: {projectPath}\n");

            // Run comprehensive validation
            var validator = new AutomatedValidationSystem(projectPath);
            var report = await validator.RunFullValidationAsync();

            // Display results
            Console.WriteLine("\n" + new string('=', 70));
            Console.WriteLine("📊 VALIDATION COMPLETE");
            Console.WriteLine(new string('=', 70));

            if (report.IssuesFound == 0)
            {
                Console.WriteLine("✅ EXCELLENT! No issues found. Your application is healthy!");
            }
            else
            {
                Console.WriteLine($"⚠️  Found {report.IssuesFound} issues that need attention:");
                Console.WriteLine($"   🔴 Critical: Issues that prevent proper functioning");
                Console.WriteLine($"   🟠 High: Issues that could cause runtime problems");
                Console.WriteLine($"   🟡 Medium: Issues that affect code quality");
                Console.WriteLine($"   🔵 Low: Minor improvements");

                Console.WriteLine($"\n🎯 Priority Actions:");
                Console.WriteLine($"   1. Fix critical issues FIRST");
                Console.WriteLine($"   2. Address high priority issues");
                Console.WriteLine($"   3. Plan medium priority fixes");
                Console.WriteLine($"   4. Consider low priority improvements");
            }

            Console.WriteLine($"\n📄 Detailed report with fix recommendations:");
            Console.WriteLine($"   {System.IO.Path.Combine(projectPath, "validation_report.html")}");
            
            Console.WriteLine($"\n⏱️  Analysis completed in {report.Duration.TotalSeconds:F1} seconds");
            Console.WriteLine($"🔄 Re-run this tool after making fixes to validate improvements");

            if (!report.BuildSucceeded)
            {
                Console.WriteLine("\n🔥 CRITICAL: Build is failing! Fix compilation errors first.");
                Environment.Exit(1);
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Validation failed: {ex.Message}");
            Console.WriteLine("Please check the error details and try again.");
            Environment.Exit(1);
        }
    }
}
}
