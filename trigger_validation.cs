using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

// Simple validation trigger to run the AutomatedValidationSystem directly
class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            Console.WriteLine("Triggering validation system...");
            
            // Load the APEX assembly
            var apexPath = @"c:\Users\Gaming\APEXv2\ApexV2\bin\Debug\net9.0-windows\ApexV2.dll";
            if (!File.Exists(apexPath))
            {
                Console.WriteLine($"APEX assembly not found at: {apexPath}");
                return;
            }
            
            var assembly = Assembly.LoadFrom(apexPath);
            var validationType = assembly.GetType("ApexV2.Validation.AutomatedValidationSystem");
            
            if (validationType == null)
            {
                Console.WriteLine("AutomatedValidationSystem type not found");
                return;
            }
            
            var validationInstance = Activator.CreateInstance(validationType);
            var method = validationType.GetMethod("RunFullValidationAsync");
            
            if (method != null)
            {
                var task = (Task)method.Invoke(validationInstance, null);
                await task;
                Console.WriteLine("Validation completed!");
            }
            else
            {
                Console.WriteLine("RunFullValidationAsync method not found");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}
