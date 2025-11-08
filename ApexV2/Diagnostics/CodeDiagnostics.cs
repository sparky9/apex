using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;

namespace ApexV2.Diagnostics
{
    /// <summary>
    /// Comprehensive diagnostic tool to identify and catalog all issues in the APEX V2 codebase
    /// </summary>
    public class CodeDiagnostics
    {
        private readonly string _projectPath;
        private readonly List<DiagnosticIssue> _issues = new List<DiagnosticIssue>();

        public CodeDiagnostics(string projectPath)
        {
            _projectPath = projectPath;
        }

        public List<DiagnosticIssue> RunFullDiagnostics()
        {
            Console.WriteLine("🔍 Running Comprehensive Code Diagnostics...");
            
            // 1. Check for missing files and broken references
            CheckMissingFiles();
            
            // 2. Check for XAML issues
            CheckXamlIssues();
            
            // 3. Check for C# compilation issues
            CheckCSharpIssues();
            
            // 4. Check for dependency issues
            CheckDependencyIssues();
            
            // 5. Check for configuration issues
            CheckConfigurationIssues();
            
            // 6. Check for runtime issues
            CheckRuntimeIssues();

            Console.WriteLine($"📊 Diagnostics complete. Found {_issues.Count} issues.");
            
            return _issues;
        }

        private void CheckMissingFiles()
        {
            Console.WriteLine("📁 Checking for missing files...");
            
            // Check for common missing files
            var requiredFiles = new[]
            {
                "App.xaml",
                "App.xaml.cs", 
                "MainWindow.xaml",
                "MainWindow.xaml.cs",
                "ApexV2.csproj"
            };

            foreach (var file in requiredFiles)
            {
                var filePath = Path.Combine(_projectPath, file);
                if (!File.Exists(filePath))
                {
                    AddIssue(IssueSeverity.Critical, "Missing File", $"Required file missing: {file}", filePath);
                }
            }
        }

        private void CheckXamlIssues()
        {
            Console.WriteLine("🔧 Checking XAML files...");
            
            var xamlFiles = Directory.GetFiles(_projectPath, "*.xaml", SearchOption.AllDirectories);
            
            foreach (var xamlFile in xamlFiles)
            {
                try
                {
                    var content = File.ReadAllText(xamlFile);
                    
                    // Check for missing resources
                    if (content.Contains("Source=\"/") && !content.Contains("pack://"))
                    {
                        var matches = Regex.Matches(content, @"Source=""(/[^""]+)""");
                        foreach (Match match in matches)
                        {
                            AddIssue(IssueSeverity.High, "XAML Resource", 
                                $"Potentially missing resource: {match.Groups[1].Value}", xamlFile);
                        }
                    }
                    
                    // Check for empty x:Name attributes
                    if (content.Contains("x:Name=\"\""))
                    {
                        AddIssue(IssueSeverity.Medium, "XAML Syntax", 
                            "Empty x:Name attribute found", xamlFile);
                    }
                    
                    // Check for unresolved bindings
                    var bindingMatches = Regex.Matches(content, @"Binding\s+([^}]+)");
                    foreach (Match match in bindingMatches)
                    {
                        var binding = match.Groups[1].Value;
                        if (binding.Contains("Missing") || binding.Contains("Error"))
                        {
                            AddIssue(IssueSeverity.Medium, "XAML Binding", 
                                $"Potentially broken binding: {binding}", xamlFile);
                        }
                    }
                }
                catch (Exception ex)
                {
                    AddIssue(IssueSeverity.High, "XAML Parse Error", 
                        $"Could not read XAML file: {ex.Message}", xamlFile);
                }
            }
        }

        private void CheckCSharpIssues()
        {
            Console.WriteLine("⚙️ Checking C# files...");
            
            var csFiles = Directory.GetFiles(_projectPath, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains("\\bin\\") && !f.Contains("\\obj\\")).ToArray();
            
            foreach (var csFile in csFiles)
            {
                try
                {
                    var content = File.ReadAllText(csFile);
                    
                    // Check for TODO/FIXME comments
                    var todoMatches = Regex.Matches(content, @"//\s*(TODO|FIXME|HACK):?\s*(.+)", RegexOptions.IgnoreCase);
                    foreach (Match match in todoMatches)
                    {
                        AddIssue(IssueSeverity.Low, "TODO/FIXME", 
                            $"{match.Groups[1].Value}: {match.Groups[2].Value}", csFile);
                    }
                    
                    // Check for empty catch blocks
                    if (Regex.IsMatch(content, @"catch\s*\([^)]*\)\s*\{\s*\}"))
                    {
                        AddIssue(IssueSeverity.Medium, "Empty Catch", 
                            "Empty catch block found - may hide errors", csFile);
                    }
                    
                    // Check for potential null reference issues
                    var nullRefMatches = Regex.Matches(content, @"\.(\w+)\s*\?\?\s*throw");
                    foreach (Match match in nullRefMatches)
                    {
                        AddIssue(IssueSeverity.Low, "Null Check", 
                            $"Null check pattern found: {match.Value}", csFile);
                    }
                    
                    // Check for async void methods (should be async Task)
                    var asyncVoidMatches = Regex.Matches(content, @"async\s+void\s+\w+");
                    foreach (Match match in asyncVoidMatches)
                    {
                        AddIssue(IssueSeverity.Medium, "Async Void", 
                            $"async void method found (should be async Task): {match.Value}", csFile);
                    }
                    
                    // Check for missing using statements
                    if (content.Contains("List<") && !content.Contains("using System.Collections.Generic"))
                    {
                        AddIssue(IssueSeverity.Low, "Missing Using", 
                            "File uses List<> but missing using System.Collections.Generic", csFile);
                    }
                }
                catch (Exception ex)
                {
                    AddIssue(IssueSeverity.High, "C# Parse Error", 
                        $"Could not read C# file: {ex.Message}", csFile);
                }
            }
        }

        private void CheckDependencyIssues()
        {
            Console.WriteLine("📦 Checking dependencies...");
            
            var csprojPath = Path.Combine(_projectPath, "ApexV2.csproj");
            if (File.Exists(csprojPath))
            {
                try
                {
                    var content = File.ReadAllText(csprojPath);
                    
                    // Check for version conflicts
                    var packageMatches = Regex.Matches(content, @"PackageReference\s+Include=""([^""]+)""\s+Version=""([^""]+)""");
                    var packages = new Dictionary<string, string>();
                    
                    foreach (Match match in packageMatches)
                    {
                        var package = match.Groups[1].Value;
                        var version = match.Groups[2].Value;
                        
                        if (packages.ContainsKey(package) && packages[package] != version)
                        {
                            AddIssue(IssueSeverity.High, "Version Conflict", 
                                $"Package {package} has multiple versions: {packages[package]} and {version}", csprojPath);
                        }
                        else
                        {
                            packages[package] = version;
                        }
                    }
                    
                    // Check for missing essential packages
                    var essentialPackages = new[] { "Microsoft.EntityFrameworkCore", "Microsoft.Extensions.DependencyInjection" };
                    foreach (var essential in essentialPackages)
                    {
                        if (!content.Contains(essential))
                        {
                            AddIssue(IssueSeverity.Medium, "Missing Package", 
                                $"Essential package may be missing: {essential}", csprojPath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    AddIssue(IssueSeverity.High, "Project File Error", 
                        $"Could not read project file: {ex.Message}", csprojPath);
                }
            }
            else
            {
                AddIssue(IssueSeverity.Critical, "Missing Project File", 
                    "ApexV2.csproj file not found", _projectPath);
            }
        }

        private void CheckConfigurationIssues()
        {
            Console.WriteLine("⚙️ Checking configuration...");
            
            // Check for appsettings files
            var configFiles = new[] { "appsettings.json", "app.config" };
            bool hasConfig = false;
            
            foreach (var configFile in configFiles)
            {
                var configPath = Path.Combine(_projectPath, configFile);
                if (File.Exists(configPath))
                {
                    hasConfig = true;
                    try
                    {
                        var content = File.ReadAllText(configPath);
                        
                        // Check for placeholder values
                        if (content.Contains("your_api_key_here") || content.Contains("placeholder"))
                        {
                            AddIssue(IssueSeverity.Medium, "Configuration", 
                                "Configuration file contains placeholder values", configPath);
                        }
                        
                        // Check for empty connection strings
                        if (content.Contains("ConnectionString") && content.Contains("\"\""))
                        {
                            AddIssue(IssueSeverity.High, "Configuration", 
                                "Empty connection string found", configPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        AddIssue(IssueSeverity.Medium, "Configuration Error", 
                            $"Could not read config file: {ex.Message}", configPath);
                    }
                }
            }
            
            if (!hasConfig)
            {
                AddIssue(IssueSeverity.Low, "No Configuration", 
                    "No configuration files found", _projectPath);
            }
        }

        private void CheckRuntimeIssues()
        {
            Console.WriteLine("🏃 Checking for potential runtime issues...");
            
            // Check for hardcoded paths
            var csFiles = Directory.GetFiles(_projectPath, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains("\\bin\\") && !f.Contains("\\obj\\")).ToArray();
            
            foreach (var csFile in csFiles)
            {
                try
                {
                    var content = File.ReadAllText(csFile);
                    
                    // Check for hardcoded Windows paths
                    var pathMatches = Regex.Matches(content, @"""[C-Z]:\\[^""]+""");
                    foreach (Match match in pathMatches)
                    {
                        AddIssue(IssueSeverity.Medium, "Hardcoded Path", 
                            $"Hardcoded Windows path found: {match.Value}", csFile);
                    }
                    
                    // Check for potential memory leaks (event handlers without unsubscribe)
                    var eventMatches = Regex.Matches(content, @"(\w+)\s*\+=\s*(\w+);");
                    foreach (Match match in eventMatches)
                    {
                        var eventName = match.Groups[1].Value;
                        var handlerName = match.Groups[2].Value;
                        
                        // Look for corresponding unsubscribe
                        if (!content.Contains($"{eventName} -= {handlerName}"))
                        {
                            AddIssue(IssueSeverity.Low, "Potential Memory Leak", 
                                $"Event subscription without unsubscribe: {eventName} += {handlerName}", csFile);
                        }
                    }
                }
                catch (Exception ex)
                {
                    AddIssue(IssueSeverity.Low, "Runtime Check Error", 
                        $"Could not analyze file for runtime issues: {ex.Message}", csFile);
                }
            }
        }

        private void AddIssue(IssueSeverity severity, string category, string description, string filePath)
        {
            _issues.Add(new DiagnosticIssue
            {
                Severity = severity,
                Category = category,
                Description = description,
                FilePath = filePath,
                Timestamp = DateTime.Now
            });
            
            var severityColor = severity switch
            {
                IssueSeverity.Critical => "🔴",
                IssueSeverity.High => "🟠", 
                IssueSeverity.Medium => "🟡",
                IssueSeverity.Low => "🔵",
                _ => "⚪"
            };
            
            Console.WriteLine($"{severityColor} [{severity}] {category}: {description}");
            if (!string.IsNullOrEmpty(filePath))
            {
                Console.WriteLine($"   📁 {Path.GetRelativePath(_projectPath, filePath)}");
            }
        }

        public void GenerateReport()
        {
            var reportPath = Path.Combine(_projectPath, "DiagnosticReport.html");
            
            var html = $@"
<!DOCTYPE html>
<html>
<head>
    <title>APEX V2 Diagnostic Report</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 20px; }}
        .header {{ background: #2c3e50; color: white; padding: 20px; border-radius: 5px; }}
        .summary {{ background: #ecf0f1; padding: 15px; margin: 20px 0; border-radius: 5px; }}
        .critical {{ border-left: 4px solid #e74c3c; }}
        .high {{ border-left: 4px solid #f39c12; }}
        .medium {{ border-left: 4px solid #f1c40f; }}
        .low {{ border-left: 4px solid #3498db; }}
        .issue {{ margin: 10px 0; padding: 15px; background: #f8f9fa; border-radius: 5px; }}
    </style>
</head>
<body>
    <div class='header'>
        <h1>🔍 APEX V2 Diagnostic Report</h1>
        <p>Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>
        <p>Total Issues Found: {_issues.Count}</p>
    </div>
    
    <div class='summary'>
        <h2>📊 Issue Summary</h2>
        <p>🔴 Critical: {_issues.Count(i => i.Severity == IssueSeverity.Critical)}</p>
        <p>🟠 High: {_issues.Count(i => i.Severity == IssueSeverity.High)}</p>
        <p>🟡 Medium: {_issues.Count(i => i.Severity == IssueSeverity.Medium)}</p>
        <p>🔵 Low: {_issues.Count(i => i.Severity == IssueSeverity.Low)}</p>
    </div>
    
    <h2>🔍 Detailed Issues</h2>
    {string.Join("", _issues.OrderBy(i => i.Severity).Select(i => $@"
    <div class='issue {i.Severity.ToString().ToLower()}'>
        <h3>{i.Category}</h3>
        <p><strong>Severity:</strong> {i.Severity}</p>
        <p><strong>Description:</strong> {i.Description}</p>
        <p><strong>File:</strong> {Path.GetRelativePath(_projectPath, i.FilePath)}</p>
        <p><strong>Time:</strong> {i.Timestamp:HH:mm:ss}</p>
    </div>"))}
</body>
</html>";

            File.WriteAllText(reportPath, html);
            Console.WriteLine($"📄 Diagnostic report saved to: {reportPath}");
        }
    }

    public class DiagnosticIssue
    {
        public IssueSeverity Severity { get; set; }
        public string Category { get; set; } = "";
        public string Description { get; set; } = "";
        public string FilePath { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }

    public enum IssueSeverity
    {
        Low,
        Medium, 
        High,
        Critical
    }
}
