using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ApexV2;

namespace ApexV2.Diagnostics
{
    public static class SimpleDiagnostic
    {
        public static void RunBasicCheck(MainWindow window)
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== APEX V2 BASIC DIAGNOSTIC ===");
            report.AppendLine($"Time: {DateTime.Now}");
            report.AppendLine();
            
            // Test 1: Theme Resources
            report.AppendLine("1. THEME RESOURCES:");
            try
            {
                var textBrush = Application.Current.TryFindResource("Brush.TextPrimary");
                if (textBrush != null)
                {
                    report.AppendLine($"   ✓ Brush.TextPrimary found: {textBrush}");
                }
                else
                {
                    report.AppendLine("   ✗ Brush.TextPrimary NOT FOUND!");
                }
                
                var panelBrush = Application.Current.TryFindResource("Brush.PanelBackground");
                if (panelBrush != null)
                {
                    report.AppendLine($"   ✓ Brush.PanelBackground found: {panelBrush}");
                }
                else
                {
                    report.AppendLine("   ✗ Brush.PanelBackground NOT FOUND!");
                }
            }
            catch (Exception ex)
            {
                report.AppendLine($"   ✗ Theme error: {ex.Message}");
            }
            
            // Test 2: Window State
            report.AppendLine();
            report.AppendLine("2. WINDOW STATE:");
            report.AppendLine($"   Title: {window.Title}");
            report.AppendLine($"   Size: {window.Width}x{window.Height}");
            report.AppendLine($"   Background: {window.Background}");
            
            // Test 3: Key UI Elements
            report.AppendLine();
            report.AppendLine("3. UI ELEMENTS:");
            
            var searchBox = window.FindName("QuickSymbolSearch") as TextBox;
            if (searchBox != null)
            {
                report.AppendLine($"   ✓ Symbol search box found - Visible: {searchBox.Visibility}");
            }
            else
            {
                report.AppendLine("   ✗ Symbol search box NOT FOUND!");
            }
            
            var statusText = window.FindName("StatusText") as TextBlock;
            if (statusText != null)
            {
                report.AppendLine($"   ✓ Status text found: '{statusText.Text}'");
            }
            else
            {
                report.AppendLine("   ✗ Status text NOT FOUND!");
            }
            
            // Write to file
            var reportPath = Path.Combine(Environment.CurrentDirectory, "diagnostic_report.txt");
            File.WriteAllText(reportPath, report.ToString());
            
            // Also try to show message
            try
            {
                MessageBox.Show($"Diagnostic complete! Report saved to:\n{reportPath}\n\nCheck console output for details.", 
                    "Diagnostic Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch { }
        }
    }
}
