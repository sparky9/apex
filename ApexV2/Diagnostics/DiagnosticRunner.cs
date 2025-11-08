using System;
using System.IO;
using ApexV2.Diagnostics;

namespace ApexV2.Diagnostics
{
    public class DiagnosticRunner
    {
        public static void Run(string[] args)
        {
            Console.WriteLine("🔍 APEX V2 Diagnostic Tool");
            Console.WriteLine("=========================");
            Console.WriteLine("This tool will scan the entire codebase and identify all issues automatically.\n");

            try
            {
                // Get the project root directory
            var currentDir = Directory.GetCurrentDirectory();
            var projectPath = Path.GetFullPath(Path.Combine(currentDir, ".."));
            
            Console.WriteLine($"📁 Scanning project: {projectPath}\n");

            // Run comprehensive diagnostics
            var diagnostics = new CodeDiagnostics(projectPath);
            var issues = diagnostics.RunFullDiagnostics();

            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("📊 DIAGNOSTIC SUMMARY");
            Console.WriteLine(new string('=', 60));

            if (issues.Count == 0)
            {
                Console.WriteLine("✅ No issues found! The codebase appears to be healthy.");
            }
            else
            {
                var critical = issues.Count(i => i.Severity == IssueSeverity.Critical);
                var high = issues.Count(i => i.Severity == IssueSeverity.High);
                var medium = issues.Count(i => i.Severity == IssueSeverity.Medium);
                var low = issues.Count(i => i.Severity == IssueSeverity.Low);

                Console.WriteLine($"📈 Total Issues Found: {issues.Count}");
                Console.WriteLine($"🔴 Critical Issues: {critical}");
                Console.WriteLine($"🟠 High Priority: {high}");
                Console.WriteLine($"🟡 Medium Priority: {medium}");
                Console.WriteLine($"🔵 Low Priority: {low}");

                if (critical > 0)
                {
                    Console.WriteLine("\n⚠️  CRITICAL ISSUES MUST BE FIXED FIRST!");
                }
                else if (high > 0)
                {
                    Console.WriteLine("\n⚠️  High priority issues should be addressed soon.");
                }

                // Generate detailed HTML report
                diagnostics.GenerateReport();
            }

            Console.WriteLine("\n✅ Diagnostic scan complete!");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error during diagnostics: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}
}
