using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ApexV2;
using ApexV2.Windows;

namespace ApexV2.Diagnostics
{
    public class UIFunctionalityTest
    {
        public static void RunDiagnostics(MainWindow mainWindow)
        {
            Console.WriteLine("=== APEX V2 UI Functionality Diagnostic ===");
            Console.WriteLine($"Timestamp: {DateTime.Now}");
            Console.WriteLine();

            // Test 1: Theme and Color Issues
            TestThemeApplication(mainWindow);
            
            // Test 2: Menu Functionality
            TestMenuFunctionality(mainWindow);
            
            // Test 3: Settings/Preferences
            TestSettingsDialogs(mainWindow);
            
            // Test 4: Chart Creation
            TestChartCreation(mainWindow);
            
            // Test 5: Symbol Search
            TestSymbolSearch(mainWindow);
            
            // Test 6: General UI Elements
            TestGeneralUI(mainWindow);
            
            Console.WriteLine("=== End Diagnostic Report ===");
        }
        
        private static void TestThemeApplication(MainWindow mainWindow)
        {
            Console.WriteLine("1. THEME & COLOR TEST:");
            
            // Check if theme resources are actually loaded
            try
            {
                var brush = Application.Current.FindResource("Brush.TextPrimary") as SolidColorBrush;
                if (brush != null)
                {
                    Console.WriteLine($"   ✓ Brush.TextPrimary found: {brush.Color}");
                }
                else
                {
                    Console.WriteLine("   ✗ Brush.TextPrimary NOT FOUND - Theme not loaded!");
                }
                
                var panelBrush = Application.Current.FindResource("Brush.PanelBackground") as SolidColorBrush;
                if (panelBrush != null)
                {
                    Console.WriteLine($"   ✓ Brush.PanelBackground found: {panelBrush.Color}");
                }
                else
                {
                    Console.WriteLine("   ✗ Brush.PanelBackground NOT FOUND - Theme not loaded!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ✗ Theme resource error: {ex.Message}");
            }
            
            // Check menu foreground
            var menu = FindChild<Menu>(mainWindow, "");
            if (menu != null)
            {
                Console.WriteLine($"   Menu Foreground: {menu.Foreground}");
                Console.WriteLine($"   Menu Background: {menu.Background}");
            }
            
            Console.WriteLine();
        }
        
        private static void TestMenuFunctionality(MainWindow mainWindow)
        {
            Console.WriteLine("2. MENU FUNCTIONALITY TEST:");
            
            // This would need to be expanded to actually trigger menu events
            Console.WriteLine("   Note: Menu click testing requires UI automation - checking if handlers exist");
            
            // Check if critical menu handlers are implemented
            var menuTests = new[]
            {
                "Settings_Click",
                "Preferences_Click", 
                "NewChart_Click",
                "SearchSymbol_Click"
            };
            
            foreach (var methodName in menuTests)
            {
                var method = typeof(MainWindow).GetMethod(methodName, 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (method != null)
                {
                    Console.WriteLine($"   ✓ {methodName} handler exists");
                }
                else
                {
                    Console.WriteLine($"   ✗ {methodName} handler MISSING");
                }
            }
            
            Console.WriteLine();
        }
        
        private static void TestSettingsDialogs(MainWindow mainWindow)
        {
            Console.WriteLine("3. SETTINGS/PREFERENCES TEST:");
            
            try
            {
                // Try to instantiate SettingsWindow
                var settingsWindow = new SettingsWindow();
                Console.WriteLine("   ✓ SettingsWindow can be created");
                settingsWindow.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ✗ SettingsWindow creation failed: {ex.Message}");
            }
            
            Console.WriteLine();
        }
        
        private static void TestChartCreation(MainWindow mainWindow)
        {
            Console.WriteLine("4. CHART CREATION TEST:");
            
            try
            {
                // Try to create a chart window
                var chartWindow = new ApexV2.Charts.Windows.ChartWindow("TEST");
                Console.WriteLine("   ✓ ChartWindow can be created");
                chartWindow.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ✗ ChartWindow creation failed: {ex.Message}");
            }
            
            Console.WriteLine();
        }
        
        private static void TestSymbolSearch(MainWindow mainWindow)
        {
            Console.WriteLine("5. SYMBOL SEARCH TEST:");
            
            var searchBox = FindChild<TextBox>(mainWindow, "QuickSymbolSearch");
            if (searchBox != null)
            {
                Console.WriteLine("   ✓ Symbol search TextBox found");
                Console.WriteLine($"   Search box visibility: {searchBox.Visibility}");
                Console.WriteLine($"   Search box enabled: {searchBox.IsEnabled}");
            }
            else
            {
                Console.WriteLine("   ✗ Symbol search TextBox NOT FOUND");
            }
            
            Console.WriteLine();
        }
        
        private static void TestGeneralUI(MainWindow mainWindow)
        {
            Console.WriteLine("6. GENERAL UI TEST:");
            
            Console.WriteLine($"   Window State: {mainWindow.WindowState}");
            Console.WriteLine($"   Window Size: {mainWindow.Width}x{mainWindow.Height}");
            Console.WriteLine($"   Window Background: {mainWindow.Background}");
            
            // Count visible UI elements
            var buttons = FindChildren<Button>(mainWindow);
            var textBlocks = FindChildren<TextBlock>(mainWindow);
            var menus = FindChildren<Menu>(mainWindow);
            
            Console.WriteLine($"   Buttons found: {buttons.Count}");
            Console.WriteLine($"   TextBlocks found: {textBlocks.Count}");
            Console.WriteLine($"   Menus found: {menus.Count}");
            
            Console.WriteLine();
        }
        
        // Helper methods for finding UI elements
        private static T FindChild<T>(DependencyObject parent, string name) where T : DependencyObject
        {
            if (parent == null) return null;
            
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T typedChild && (string.IsNullOrEmpty(name) || 
                    (child is FrameworkElement fe && fe.Name == name)))
                {
                    return typedChild;
                }
                
                var result = FindChild<T>(child, name);
                if (result != null) return result;
            }
            
            return null;
        }
        
        private static List<T> FindChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            var children = new List<T>();
            
            if (parent == null) return children;
            
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T typedChild)
                {
                    children.Add(typedChild);
                }
                
                children.AddRange(FindChildren<T>(child));
            }
            
            return children;
        }
    }
}
