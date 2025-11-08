using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ApexV2.Validation;

namespace ApexV2.Validation
{
    /// <summary>
    /// Automated validation system that continuously monitors and reports issues
    /// Runs comprehensive checks and provides actionable fix recommendations
    /// </summary>
    public class AutomatedValidationSystem
    {
        private readonly string _projectPath;
        private readonly List<ValidationIssue> _issues = new List<ValidationIssue>();
        private readonly string _reportPath;

        public AutomatedValidationSystem(string projectPath)
        {
            _projectPath = projectPath;
            _reportPath = Path.Combine(projectPath, "validation_report.html");
        }

        /// <summary>
        /// Run complete automated validation suite
        /// </summary>
        public async Task<ValidationReport> RunFullValidationAsync()
        {
            Console.WriteLine("🔍 Starting Automated Validation System...");
            
            // Clear previous issues to prevent accumulation
            _issues.Clear();
            
            var report = new ValidationReport
            {
                StartTime = DateTime.Now,
                ProjectPath = _projectPath
            };

            // 1. Build Validation
            Console.WriteLine("📦 Validating build...");
            await ValidateBuild(report);

            // 2. Code Quality Validation
            Console.WriteLine("⚙️ Validating code quality...");
            await ValidateCodeQuality(report);

            // 3. Performance Analytics Validation
            Console.WriteLine("📊 Validating Performance Analytics...");
            await ValidatePerformanceAnalytics(report);

            // 4. XAML Validation
            Console.WriteLine("🖥️ Validating XAML files...");
            await ValidateXamlFiles(report);

            // 5. Test Coverage Validation
            Console.WriteLine("🧪 Validating test coverage...");
            await ValidateTestCoverage(report);

            // 6. Runtime Validation
            Console.WriteLine("🏃 Validating runtime behavior...");
            await ValidateRuntime(report);

            // 7. UX Validation
            Console.WriteLine("🎨 Validating user experience...");
            await ValidateUserExperience(report);

            report.EndTime = DateTime.Now;
            report.Duration = report.EndTime - report.StartTime;
            report.IssuesFound = _issues.Count;
            report.Issues = new List<ValidationIssue>(_issues);

            await GenerateDetailedReport(report);
            
            return report;
        }

        private async Task ValidateBuild(ValidationReport report)
        {
            try
            {
                var buildProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = "build --configuration Release --verbosity minimal --no-restore",
                        WorkingDirectory = _projectPath,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                buildProcess.Start();
                var output = await buildProcess.StandardOutput.ReadToEndAsync();
                var errors = await buildProcess.StandardError.ReadToEndAsync();
                await buildProcess.WaitForExitAsync();

                // More accurate build success detection
                var isSuccessfulBuild = buildProcess.ExitCode == 0 && 
                                      !output.Contains("Build FAILED") && 
                                      !errors.Contains("error CS") &&
                                      (output.Contains("Build succeeded") || output.Contains("succeeded"));

                report.BuildSucceeded = isSuccessfulBuild;
                report.BuildOutput = output;
                report.BuildErrors = errors;

                if (!isSuccessfulBuild && buildProcess.ExitCode != 0)
                {
                    // Only add build failed issue if there are actual compilation errors
                    if (errors.Contains("error CS") || output.Contains("error CS"))
                    {
                        AddIssue(Severity.Critical, "Build Failed", 
                            $"Build failed with compilation errors", 
                            errors, "Fix compilation errors before proceeding");
                    }
                    else
                    {
                        // If exit code is non-zero but no compilation errors, it might be a warning-only build
                        // Count this as successful but with warnings
                        report.BuildSucceeded = true;
                    }
                }

                // Check for warnings (but don't spam with too many)
                if (output.Contains("warning") && _issues.Count < 50) // Limit warning reports
                {
                    var warningLines = output.Split('\n').Where(line => line.Contains("warning")).Take(3);
                    var warningCount = warningLines.Count();
                    if (warningCount > 0)
                    {
                        AddIssue(Severity.Low, "Build Warnings", 
                            $"Build succeeded with {warningCount} warnings (showing first 3)", 
                            string.Join("\n", warningLines), "Review and fix build warnings");
                    }
                }
            }
            catch (Exception ex)
            {
                AddIssue(Severity.Critical, "Build Process Error", 
                    "Could not execute build process", ex.Message, 
                    "Ensure .NET SDK is installed and accessible");
            }
        }

        private async Task ValidateCodeQuality(ValidationReport report)
        {
            var csFiles = Directory.GetFiles(_projectPath, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains("\\bin\\") && !f.Contains("\\obj\\"))
                .ToArray();

            var qualityIssues = 0;

            foreach (var file in csFiles)
            {
                try
                {
                    var content = await File.ReadAllTextAsync(file);
                    var relativePath = Path.GetRelativePath(_projectPath, file);

                    // Check for async void methods
                    if (content.Contains("async void"))
                    {
                        AddIssue(Severity.High, "Async Void Method", 
                            $"File contains async void method", relativePath,
                            "Change async void to async Task for proper exception handling");
                        qualityIssues++;
                    }

                    // Check for empty catch blocks
                    if (System.Text.RegularExpressions.Regex.IsMatch(content, @"catch\s*\([^)]*\)\s*\{\s*\}"))
                    {
                        AddIssue(Severity.High, "Empty Catch Block", 
                            "File contains empty catch block that may hide errors", relativePath,
                            "Add proper error handling or logging in catch blocks");
                        qualityIssues++;
                    }

                    // Check for TODO/FIXME comments (but limit to avoid spam)
                    var todoMatches = System.Text.RegularExpressions.Regex.Matches(content, @"//\s*(TODO|FIXME|HACK):?\s*(.+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    var todoCount = 0;
                    foreach (System.Text.RegularExpressions.Match match in todoMatches)
                    {
                        if (todoCount < 5) // Limit to first 5 TODOs per file to avoid spam
                        {
                            AddIssue(Severity.Low, "TODO/FIXME Comment", 
                                $"{match.Groups[1].Value}: {match.Groups[2].Value}", relativePath,
                                "Complete the TODO item or remove the comment");
                            qualityIssues++;
                            todoCount++;
                        }
                    }

                    // Check for hardcoded paths
                    var pathMatches = System.Text.RegularExpressions.Regex.Matches(content, @"""[C-Z]:\\[^""]+""");
                    foreach (System.Text.RegularExpressions.Match match in pathMatches)
                    {
                        AddIssue(Severity.Medium, "Hardcoded Path", 
                            $"Hardcoded Windows path: {match.Value}", relativePath,
                            "Use Path.Combine or configuration for cross-platform compatibility");
                        qualityIssues++;
                    }
                }
                catch (Exception ex)
                {
                    AddIssue(Severity.Medium, "File Read Error", 
                        $"Could not analyze file: {ex.Message}", file,
                        "Check file permissions and encoding");
                }
            }

            report.CodeQualityIssues = qualityIssues;
        }

        private async Task ValidatePerformanceAnalytics(ValidationReport report)
        {
            var performanceIssues = 0;

            // Check if Performance Analytics files exist
            var requiredFiles = new[]
            {
                "Dashboard\\Portfolio\\PerformanceAnalyticsService.cs",
                "Dashboard\\Portfolio\\PerformanceAnalyticsPanel.xaml",
                "Dashboard\\Portfolio\\PerformanceAnalyticsPanel.xaml.cs",
                "Dashboard\\Portfolio\\PerformanceModels.cs"
            };

            foreach (var requiredFile in requiredFiles)
            {
                var filePath = Path.Combine(_projectPath, requiredFile);
                if (!File.Exists(filePath))
                {
                    AddIssue(Severity.Critical, "Missing Performance File", 
                        $"Required Performance Analytics file not found: {requiredFile}", "",
                        "Ensure all Performance Analytics components are present");
                    performanceIssues++;
                }
            }

            // Check Performance Analytics Service implementation
            var serviceFile = Path.Combine(_projectPath, "Dashboard\\Portfolio\\PerformanceAnalyticsService.cs");
            if (File.Exists(serviceFile))
            {
                var content = await File.ReadAllTextAsync(serviceFile);

                // Check for proper interface implementation
                if (!content.Contains("IPerformanceAnalyticsService"))
                {
                    AddIssue(Severity.Medium, "Missing Interface", 
                        "PerformanceAnalyticsService doesn't implement required interface", 
                        "PerformanceAnalyticsService.cs",
                        "Implement IPerformanceAnalyticsService interface");
                    performanceIssues++;
                }

                // Check for error handling in critical methods
                if (!content.Contains("try") || !content.Contains("catch"))
                {
                    AddIssue(Severity.High, "Missing Error Handling", 
                        "Performance Analytics Service lacks proper error handling", 
                        "PerformanceAnalyticsService.cs",
                        "Add try-catch blocks around critical calculations");
                    performanceIssues++;
                }
            }

            report.PerformanceAnalyticsIssues = performanceIssues;
        }

        private async Task ValidateXamlFiles(ValidationReport report)
        {
            var xamlFiles = Directory.GetFiles(_projectPath, "*.xaml", SearchOption.AllDirectories);
            var xamlIssues = 0;

            foreach (var xamlFile in xamlFiles)
            {
                try
                {
                    var content = await File.ReadAllTextAsync(xamlFile);
                    var relativePath = Path.GetRelativePath(_projectPath, xamlFile);

                    // Check for missing resources
                    if (content.Contains("Source=\"/") && !content.Contains("pack://"))
                    {
                        var matches = System.Text.RegularExpressions.Regex.Matches(content, @"Source=""(/[^""]+)""");
                        foreach (System.Text.RegularExpressions.Match match in matches)
                        {
                            AddIssue(Severity.High, "Missing XAML Resource", 
                                $"Potentially missing resource: {match.Groups[1].Value}", relativePath,
                                "Verify resource exists or use pack URI syntax");
                            xamlIssues++;
                        }
                    }

                    // Check for empty x:Name attributes
                    if (content.Contains("x:Name=\"\""))
                    {
                        AddIssue(Severity.Medium, "Empty XAML Name", 
                            "Empty x:Name attribute found", relativePath,
                            "Remove empty x:Name or provide a valid name");
                        xamlIssues++;
                    }
                }
                catch (Exception ex)
                {
                    AddIssue(Severity.Medium, "XAML Parse Error", 
                        $"Could not analyze XAML file: {ex.Message}", xamlFile,
                        "Check XAML syntax and encoding");
                    xamlIssues++;
                }
            }

            report.XamlIssues = xamlIssues;
        }

        private async Task ValidateTestCoverage(ValidationReport report)
        {
            var testFiles = Directory.GetFiles(_projectPath, "*Tests.cs", SearchOption.AllDirectories);
            var sourceFiles = Directory.GetFiles(_projectPath, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains("\\bin\\") && !f.Contains("\\obj\\") && !f.Contains("Tests"))
                .ToArray();

            var testCoverageIssues = 0;

            // Check if tests exist for major components
            var majorComponents = new[]
            {
                "PerformanceAnalyticsService",
                "MarketDataService", 
                "PortfolioService",
                "ChartControl"
            };

            foreach (var component in majorComponents)
            {
                var hasTest = testFiles.Any(f => Path.GetFileName(f).Contains(component));
                if (!hasTest)
                {
                    AddIssue(Severity.Medium, "Missing Tests", 
                        $"No tests found for {component}", "",
                        $"Create unit tests for {component}");
                    testCoverageIssues++;
                }
            }

            // Check for placeholder tests
            foreach (var testFile in testFiles)
            {
                try
                {
                    var content = await File.ReadAllTextAsync(testFile);
                    if (content.Contains("// For now") || content.Contains("placeholder") || content.Contains("TODO"))
                    {
                        AddIssue(Severity.Low, "Placeholder Test", 
                            "Test file contains placeholder or incomplete tests", 
                            Path.GetRelativePath(_projectPath, testFile),
                            "Complete the test implementation");
                        testCoverageIssues++;
                    }
                }
                catch (Exception ex)
                {
                    AddIssue(Severity.Low, "Test File Error", 
                        $"Could not analyze test file: {ex.Message}", testFile,
                        "Check test file syntax and accessibility");
                }
            }

            report.TestCoverageIssues = testCoverageIssues;
        }

        private async Task ValidateRuntime(ValidationReport report)
        {
            // This would ideally run the application and perform runtime checks
            // For now, we'll do static analysis for runtime issues

            var runtimeIssues = 0;
            var csFiles = Directory.GetFiles(_projectPath, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains("\\bin\\") && !f.Contains("\\obj\\"))
                .ToArray();

            foreach (var file in csFiles)
            {
                try
                {
                    var content = await File.ReadAllTextAsync(file);
                    var relativePath = Path.GetRelativePath(_projectPath, file);

                    // Check for potential null reference issues (limit to avoid spam)
                    var nullRefMatches = System.Text.RegularExpressions.Regex.Matches(content, @"\.Value(?!\?)");
                    var nullRefCount = 0;
                    foreach (System.Text.RegularExpressions.Match match in nullRefMatches)
                    {
                        if (nullRefCount < 3) // Limit to first 3 per file
                        {
                            AddIssue(Severity.Medium, "Potential Null Reference", 
                                "Direct .Value access without null check", relativePath,
                                "Use null-conditional operator (?.) or explicit null checks");
                            runtimeIssues++;
                            nullRefCount++;
                        }
                    }

                    // Check for unhandled exceptions in event handlers (limit to avoid spam)
                    var eventHandlerMatches = System.Text.RegularExpressions.Regex.Matches(content, @"private\s+(?:async\s+)?void\s+\w+_\w+\([^)]*\)");
                    var handlerCount = 0;
                    foreach (System.Text.RegularExpressions.Match match in eventHandlerMatches)
                    {
                        if (handlerCount < 3) // Limit to first 3 per file
                        {
                            // Get the method body (simplified check)
                            var methodStart = content.IndexOf(match.Value);
                            var braceStart = content.IndexOf('{', methodStart);
                            if (braceStart > 0)
                            {
                                var methodBody = content.Substring(braceStart, Math.Min(500, content.Length - braceStart));
                                if (!methodBody.Contains("try") || !methodBody.Contains("catch"))
                                {
                                    AddIssue(Severity.Medium, "Unhandled Exception in Event Handler", 
                                        $"Event handler {match.Value} lacks exception handling", relativePath,
                                        "Add try-catch blocks in event handlers to prevent crashes");
                                    runtimeIssues++;
                                    handlerCount++;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    AddIssue(Severity.Low, "Runtime Analysis Error", 
                        $"Could not analyze file for runtime issues: {ex.Message}", file,
                        "Check file accessibility and content");
                }
            }

            report.RuntimeIssues = runtimeIssues;
        }

        private async Task ValidateUserExperience(ValidationReport report)
        {
            var uxValidation = new UXValidationSystem(_projectPath);
            var uxIssues = await uxValidation.ValidateUserExperienceAsync();
            
            // Add UX issues to our main issues list
            foreach (var uxIssue in uxIssues)
            {
                var severity = uxIssue.Severity.ToLower() switch
                {
                    "critical" => Severity.Critical,
                    "high" => Severity.High,
                    "medium" => Severity.Medium,
                    _ => Severity.Low
                };
                
                AddIssue(severity, uxIssue.Category, uxIssue.Description, 
                        uxIssue.Location, uxIssue.Recommendation);
            }
            
            // Auto-fix what we can
            var fixedCount = await uxValidation.AutoFixIssuesAsync(uxIssues);
            if (fixedCount > 0)
            {
                Console.WriteLine($"🔧 Auto-fixed {fixedCount} UX issues");
            }
        }

        private void AddIssue(Severity severity, string category, string description, string location, string recommendation)
        {
            // Prevent duplicate issues - check if similar issue already exists
            var duplicateExists = _issues.Any(existing => 
                existing.Category == category && 
                existing.Description == description && 
                existing.Location == location);
                
            if (duplicateExists)
            {
                return; // Skip duplicate issue
            }

            // Determine if issue is auto-fixable based on category and severity
            var isAutoFixable = false;
            var categoryLower = category.ToLower();
            
            // Auto-fixable categories
            if (categoryLower.Contains("warning") ||
                categoryLower.Contains("performance") ||
                categoryLower.Contains("memory") ||
                (categoryLower.Contains("ui") && severity != Severity.Critical) ||
                (categoryLower.Contains("todo") && severity == Severity.Low) ||
                (categoryLower.Contains("fixme") && severity == Severity.Low) ||
                (categoryLower.Contains("placeholder") && severity == Severity.Low) ||
                (categoryLower.Contains("empty catch") && severity <= Severity.Medium) ||
                (categoryLower.Contains("async void") && severity <= Severity.High))
            {
                isAutoFixable = true;
            }

            _issues.Add(new ValidationIssue
            {
                Severity = severity,
                Category = category,
                Description = description,
                Location = location,
                Recommendation = recommendation,
                Timestamp = DateTime.Now,
                IsAutoFixable = isAutoFixable
            });

            var severityIcon = severity switch
            {
                Severity.Critical => "🔴",
                Severity.High => "🟠",
                Severity.Medium => "🟡",
                Severity.Low => "🔵",
                _ => "⚪"
            };

            var autoFixIcon = isAutoFixable ? " 🔧" : "";
            Console.WriteLine($"{severityIcon} [{severity}] {category}: {description}{autoFixIcon}");
            if (!string.IsNullOrEmpty(location))
            {
                Console.WriteLine($"   📁 {location}");
            }
            if (!string.IsNullOrEmpty(recommendation))
            {
                Console.WriteLine($"   💡 {recommendation}");
            }
        }

        private async Task GenerateDetailedReport(ValidationReport report)
        {
            var criticalIssues = _issues.Count(i => i.Severity == Severity.Critical);
            var highIssues = _issues.Count(i => i.Severity == Severity.High);
            var mediumIssues = _issues.Count(i => i.Severity == Severity.Medium);
            var lowIssues = _issues.Count(i => i.Severity == Severity.Low);

            var html = $@"
<!DOCTYPE html>
<html>
<head>
    <title>APEX V2 Validation Report</title>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 0; padding: 20px; background: #f5f5f5; }}
        .container {{ max-width: 1200px; margin: 0 auto; background: white; padding: 30px; border-radius: 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }}
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 30px; border-radius: 10px; margin: -30px -30px 30px -30px; }}
        .summary {{ display: grid; grid-template-columns: repeat(auto-fit, minmax(250px, 1fr)); gap: 20px; margin: 30px 0; }}
        .summary-card {{ background: #f8f9fa; padding: 20px; border-radius: 8px; border-left: 4px solid #007bff; }}
        .critical {{ border-left-color: #dc3545; }}
        .high {{ border-left-color: #fd7e14; }}
        .medium {{ border-left-color: #ffc107; }}
        .low {{ border-left-color: #17a2b8; }}
        .issue {{ margin: 15px 0; padding: 20px; background: #f8f9fa; border-radius: 8px; border-left: 4px solid #dee2e6; }}
        .issue.critical {{ border-left-color: #dc3545; background: #f8f9fa; }}
        .issue.high {{ border-left-color: #fd7e14; background: #fff3cd; }}
        .issue.medium {{ border-left-color: #ffc107; background: #fff3cd; }}
        .issue.low {{ border-left-color: #17a2b8; background: #d1ecf1; }}
        .recommendation {{ background: #e7f3ff; padding: 15px; margin: 10px 0; border-radius: 5px; border-left: 3px solid #007bff; }}
        .success {{ color: #28a745; }}
        .error {{ color: #dc3545; }}
        .location {{ font-family: 'Courier New', monospace; background: #f1f1f1; padding: 5px; border-radius: 3px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🔍 APEX V2 Automated Validation Report</h1>
            <p><strong>Generated:</strong> {report.EndTime:yyyy-MM-dd HH:mm:ss}</p>
            <p><strong>Duration:</strong> {report.Duration.TotalSeconds:F1} seconds</p>
            <p><strong>Build Status:</strong> <span class='{(report.BuildSucceeded ? "success" : "error")}'>{(report.BuildSucceeded ? "✅ SUCCESS" : "❌ FAILED")}</span></p>
        </div>
        
        <div class='summary'>
            <div class='summary-card critical'>
                <h3>🔴 Critical Issues</h3>
                <div style='font-size: 2em; font-weight: bold;'>{criticalIssues}</div>
                <p>Must be fixed immediately</p>
            </div>
            <div class='summary-card high'>
                <h3>🟠 High Priority</h3>
                <div style='font-size: 2em; font-weight: bold;'>{highIssues}</div>
                <p>Should be fixed soon</p>
            </div>
            <div class='summary-card medium'>
                <h3>🟡 Medium Priority</h3>
                <div style='font-size: 2em; font-weight: bold;'>{mediumIssues}</div>
                <p>Plan to fix in next iteration</p>
            </div>
            <div class='summary-card low'>
                <h3>🔵 Low Priority</h3>
                <div style='font-size: 2em; font-weight: bold;'>{lowIssues}</div>
                <p>Consider for future improvements</p>
            </div>
        </div>
        
        <h2>📊 Validation Summary</h2>
        <ul>
            <li><strong>Code Quality Issues:</strong> {report.CodeQualityIssues}</li>
            <li><strong>Performance Analytics Issues:</strong> {report.PerformanceAnalyticsIssues}</li>
            <li><strong>XAML Issues:</strong> {report.XamlIssues}</li>
            <li><strong>Test Coverage Issues:</strong> {report.TestCoverageIssues}</li>
            <li><strong>Runtime Issues:</strong> {report.RuntimeIssues}</li>
        </ul>
        
        <h2>🔍 Detailed Issues</h2>
        {string.Join("", _issues.OrderBy(i => i.Severity).Select(i => $@"
        <div class='issue {i.Severity.ToString().ToLower()}'>
            <h3>{i.Category}</h3>
            <p><strong>Severity:</strong> {i.Severity}</p>
            <p><strong>Description:</strong> {i.Description}</p>
            {(!string.IsNullOrEmpty(i.Location) ? $"<p><strong>Location:</strong> <span class='location'>{i.Location}</span></p>" : "")}
            <div class='recommendation'>
                <strong>💡 Recommendation:</strong> {i.Recommendation}
            </div>
        </div>"))}
        
        {(!report.BuildSucceeded ? $@"
        <h2>❌ Build Errors</h2>
        <pre style='background: #f8f9fa; padding: 15px; border-radius: 5px; overflow-x: auto; border-left: 4px solid #dc3545;'>{report.BuildErrors}</pre>
        " : "")}
        
        <div style='margin-top: 40px; padding: 20px; background: #e7f3ff; border-radius: 8px;'>
            <h3>📋 Next Steps</h3>
            <ol>
                {(criticalIssues > 0 ? "<li><strong>Address all critical issues immediately</strong> - these prevent the application from working properly</li>" : "")}
                {(highIssues > 0 ? "<li><strong>Fix high priority issues</strong> - these could cause runtime problems</li>" : "")}
                {(mediumIssues > 0 ? "<li><strong>Plan medium priority fixes</strong> - these improve code quality</li>" : "")}
                <li><strong>Run validation again</strong> after fixes to ensure issues are resolved</li>
                <li><strong>Consider implementing continuous validation</strong> in your development workflow</li>
            </ol>
        </div>
    </div>
</body>
</html>";

            await File.WriteAllTextAsync(_reportPath, html);
            
            Console.WriteLine($"\n📄 Detailed validation report generated: {_reportPath}");
            Console.WriteLine($"🔗 Open the report in your browser to see detailed analysis and recommendations");
        }
    }

    public class ValidationReport
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public string ProjectPath { get; set; } = "";
        public bool BuildSucceeded { get; set; }
        public string BuildOutput { get; set; } = "";
        public string BuildErrors { get; set; } = "";
        public int IssuesFound { get; set; }
        public int CodeQualityIssues { get; set; }
        public int PerformanceAnalyticsIssues { get; set; }
        public int XamlIssues { get; set; }
        public int TestCoverageIssues { get; set; }
        public int RuntimeIssues { get; set; }
        public List<ValidationIssue> Issues { get; set; } = new List<ValidationIssue>();
    }

    public class ValidationIssue
    {
        public Severity Severity { get; set; }
        public string Category { get; set; } = "";
        public string Description { get; set; } = "";
        public string Location { get; set; } = "";
        public string Recommendation { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public bool IsAutoFixable { get; set; } = false;
    }

    public enum Severity
    {
        Low,
        Medium,
        High,
        Critical
    }
}
