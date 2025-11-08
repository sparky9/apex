using System;
using System.Threading.Tasks;

namespace ApexV2
{
    public class TestConsole
    {
        public static async Task Run(string[] args)
        {
            await SystemTest.RunCoreTests();
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}
