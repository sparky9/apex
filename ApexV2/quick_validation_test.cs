using System;
using System.Threading.Tasks;
using ApexV2.Validation;

class Program
{
    static async Task Main(string[] args)
    {
        var projectPath = @"c:\Users\Gaming\APEXv2\ApexV2";
        var validationSystem = new AutomatedValidationSystem(projectPath);
        
        Console.WriteLine("🔍 Running validation system...");
        var report = await validationSystem.RunFullValidationAsync();
        
        Console.WriteLine($"\n📊 VALIDATION RESULTS:");
        Console.WriteLine($"Build Status: {(report.BuildSucceeded ? "SUCCESS" : "FAILED")}");
        Console.WriteLine($"Total Issues: {report.IssuesFound}");
        Console.WriteLine($"Duration: {report.Duration.TotalSeconds:F1}s");
        
        Console.WriteLine($"\nValidation report saved to: validation_report.html");
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }
}
