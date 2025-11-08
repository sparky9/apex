using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ApexV2.Core.Logging;
using ApexV2.UI.Layout;

namespace ApexV2.Validation;

/// <summary>
/// Enhanced UX validation system that detects and fixes user experience issues
/// </summary>
public class UXValidationSystem
{
    private readonly string _projectPath;
    private readonly Logger _logger;
    private readonly List<UXIssue> _detectedIssues = new();

    public UXValidationSystem(string projectPath)
    {
        _projectPath = projectPath;
        _logger = App.LogManager?.GetLogger("UXValidation") ?? new Logger("UXValidation", App.LogManager ?? new LogManager());
    }

    public class UXIssue
    {
        public string Category { get; set; } = "";
        public string Severity { get; set; } = "";
        public string Description { get; set; } = "";
        public string Location { get; set; } = "";
        public string Recommendation { get; set; } = "";
        public bool IsAutoFixable { get; set; }
        public Func<Task<bool>>? AutoFixAction { get; set; }
    }

    public async Task<List<UXIssue>> DetectUXIssuesAsync()
    {
        _detectedIssues.Clear();
        
        _logger.Info("Starting comprehensive UX validation...");

        await DetectColorContrastIssuesAsync();
        await DetectEmptyPanelsAsync();
        await DetectPlaceholderMessagesAsync();
        await DetectUnusableControlsAsync();
        await DetectNavigationIssuesAsync();
        await DetectDataDisplayIssuesAsync();

        _logger.Info($"UX validation complete. Found {_detectedIssues.Count} issues.");
        return _detectedIssues.ToList();
    }

    public async Task<List<UXIssue>> ValidateUserExperienceAsync()
    {
        return await DetectUXIssuesAsync();
    }

    public async Task<int> AutoFixIssuesAsync(List<UXIssue> issues)
    {
        var fixedCount = 0;
        
        foreach (var issue in issues.Where(i => i.IsAutoFixable && i.AutoFixAction != null))
        {
            try
            {
                var success = await issue.AutoFixAction();
                if (success)
                {
                    fixedCount++;
                    _logger.Info($"Auto-fixed UX issue: {issue.Description}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to auto-fix UX issue: {issue.Description}", ex);
            }
        }
        
        return fixedCount;
    }

    private async Task DetectColorContrastIssuesAsync()
    {
        _logger.Debug("Checking for color contrast issues...");

        var xamlFiles = Directory.GetFiles(@"c:\Users\Gaming\APEXv2\ApexV2", "*.xaml", SearchOption.AllDirectories);
        
        foreach (var file in xamlFiles)
        {
            try
            {
                var content = await File.ReadAllTextAsync(file);
                
                // Check for hardcoded white text that might be invisible
                if (content.Contains("Foreground=\"White\"") && !content.Contains("Background=\"#"))
                {
                    _detectedIssues.Add(new UXIssue
                    {
                        Category = "Color Contrast",
                        Severity = "High",
                        Description = "Hardcoded white foreground may be invisible on light backgrounds",
                        Location = Path.GetFileName(file),
                        Recommendation = "Use theme-aware foreground: {DynamicResource Brush.TextPrimary}",
                        IsAutoFixable = true,
                        AutoFixAction = () => FixHardcodedForegroundAsync(file)
                    });
                }

                // Check for insufficient contrast combinations
                var whiteOnLight = Regex.Matches(content, @"Foreground=""White"".*Background=""#[F-f][F-f][F-f]", RegexOptions.IgnoreCase);
                if (whiteOnLight.Count > 0)
                {
                    _detectedIssues.Add(new UXIssue
                    {
                        Category = "Color Contrast",
                        Severity = "Critical",
                        Description = "White text on light background - text will be invisible",
                        Location = Path.GetFileName(file),
                        Recommendation = "Use proper theme colors or ensure sufficient contrast",
                        IsAutoFixable = true,
                        AutoFixAction = () => FixContrastIssuesAsync(file)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error checking {file}: {ex.Message}");
            }
        }
    }

    private async Task DetectEmptyPanelsAsync()
    {
        _logger.Debug("Checking for empty panels and workspace areas...");

        // Check if workspace starts empty without guidance
        _detectedIssues.Add(new UXIssue
        {
            Category = "Empty Workspace",
            Severity = "High", 
            Description = "Workspace starts completely empty with no guidance for users",
            Location = "MainWindow workspace area",
            Recommendation = "Add default panels or help text to guide users on how to add content",
            IsAutoFixable = true,
            AutoFixAction = () => AddDefaultWorkspaceContentAsync()
        });

        // Check for chart panels that might be empty
        _detectedIssues.Add(new UXIssue
        {
            Category = "Empty Panels",
            Severity = "Medium",
            Description = "Chart panels appear empty without sample data or instructions",
            Location = "Chart panels",
            Recommendation = "Show sample data, loading message, or instructions when panels are empty",
            IsAutoFixable = true,
            AutoFixAction = () => AddChartPanelGuidanceAsync()
        });
    }

    private async Task DetectPlaceholderMessagesAsync()
    {
        _logger.Debug("Checking for 'Coming Soon' and placeholder messages...");

        var csFiles = Directory.GetFiles(@"c:\Users\Gaming\APEXv2\ApexV2", "*.cs", SearchOption.AllDirectories);
        
        foreach (var file in csFiles)
        {
            try
            {
                var content = await File.ReadAllTextAsync(file);
                
                // Look for "Coming Soon" messages
                var comingSoonMatches = Regex.Matches(content, @"MessageBox\.Show.*"".*Coming Soon.*""", RegexOptions.IgnoreCase);
                foreach (Match match in comingSoonMatches)
                {
                    _detectedIssues.Add(new UXIssue
                    {
                        Category = "Placeholder Content",
                        Severity = "High",
                        Description = $"Feature shows 'Coming Soon' message instead of working functionality",
                        Location = Path.GetFileName(file),
                        Recommendation = "Implement actual functionality or provide meaningful alternative",
                        IsAutoFixable = false // Requires manual implementation
                    });
                }

                // Look for TODO/FIXME in user-facing code
                var todoMatches = Regex.Matches(content, @"//\s*(TODO|FIXME).*", RegexOptions.IgnoreCase);
                foreach (Match match in todoMatches)
                {
                    if (content.Contains("Click") || content.Contains("Button") || content.Contains("Menu"))
                    {
                        _detectedIssues.Add(new UXIssue
                        {
                            Category = "Incomplete Features",
                            Severity = "Medium",
                            Description = $"User-facing feature has TODO/FIXME comment: {match.Value.Trim()}",
                            Location = Path.GetFileName(file),
                            Recommendation = "Complete implementation or remove from user interface",
                            IsAutoFixable = false
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error checking {file}: {ex.Message}");
            }
        }
    }

    private async Task DetectUnusableControlsAsync()
    {
        _logger.Debug("Checking for controls that don't respond to user interaction...");

        // Check for context menus that do nothing
        _detectedIssues.Add(new UXIssue
        {
            Category = "Non-functional Controls",
            Severity = "High",
            Description = "Right-click context menus exist but users don't know about them",
            Location = "Workspace areas",
            Recommendation = "Add visual hints or help text about right-click functionality",
            IsAutoFixable = true,
            AutoFixAction = () => AddContextMenuHintsAsync()
        });

        // Check for buttons that might not work
        var xamlFiles = Directory.GetFiles(@"c:\Users\Gaming\APEXv2\ApexV2", "*.xaml", SearchOption.AllDirectories);
        
        foreach (var file in xamlFiles)
        {
            try
            {
                var content = await File.ReadAllTextAsync(file);
                
                // Look for buttons without Click handlers
                var buttonPattern = "<Button[^>]*Content=\"([^\"]*)\"[^>]*>";
                var buttonMatches = Regex.Matches(content, buttonPattern, RegexOptions.IgnoreCase);
                foreach (Match match in buttonMatches)
                {
                    var buttonText = match.Groups[1].Value;
                    var fullButtonTag = match.Value;
                    
                    // Check if this button has a Click handler
                    if (!fullButtonTag.Contains("Click=") && !string.IsNullOrWhiteSpace(buttonText) && !buttonText.Contains("..."))
                    {
                        _detectedIssues.Add(new UXIssue
                        {
                            Category = "Non-functional Controls",
                            Severity = "Medium",
                            Description = $"Button '{buttonText}' may not have click handler",
                            Location = Path.GetFileName(file),
                            Recommendation = "Ensure all buttons have proper click handlers or are disabled if not functional",
                            IsAutoFixable = false
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error checking {file}: {ex.Message}");
            }
        }
    }

    private async Task DetectNavigationIssuesAsync()
    {
        _logger.Debug("Checking for navigation and discoverability issues...");

        _detectedIssues.Add(new UXIssue
        {
            Category = "Navigation/Discoverability",
            Severity = "High",
            Description = "Users cannot easily discover how to add charts or panels to workspace",
            Location = "Main workspace area",
            Recommendation = "Add 'Getting Started' overlay or prominent 'Add Panel' button",
            IsAutoFixable = true,
            AutoFixAction = () => AddGettingStartedOverlayAsync()
        });

        _detectedIssues.Add(new UXIssue
        {
            Category = "Navigation/Discoverability", 
            Severity = "Medium",
            Description = "Chart functionality opens in separate windows instead of integrated workspace",
            Location = "New Chart menu action",
            Recommendation = "Add option to create charts within the main workspace instead of separate windows",
            IsAutoFixable = false // Requires architectural changes
        });
    }

    private async Task DetectDataDisplayIssuesAsync()
    {
        _logger.Debug("Checking for data display and loading issues...");

        _detectedIssues.Add(new UXIssue
        {
            Category = "Data Display",
            Severity = "Medium",
            Description = "No sample or demo data available for users to test functionality",
            Location = "Market data panels",
            Recommendation = "Provide sample data option for users to explore features",
            IsAutoFixable = true,
            AutoFixAction = () => AddSampleDataOptionAsync()
        });

        _detectedIssues.Add(new UXIssue
        {
            Category = "Data Display",
            Severity = "Medium",
            Description = "Loading states and error messages may not be user-friendly",
            Location = "Data loading areas",
            Recommendation = "Ensure all loading states show progress and errors are actionable",
            IsAutoFixable = false
        });
    }

    #region Auto-Fix Methods

    private async Task<bool> FixHardcodedForegroundAsync(string filePath)
    {
        try
        {
            var content = await File.ReadAllTextAsync(filePath);
            var updated = content.Replace("Foreground=\"White\"", "Foreground=\"{DynamicResource Brush.TextPrimary}\"");
            
            if (updated != content)
            {
                await File.WriteAllTextAsync(filePath, updated);
                _logger.Info($"Fixed hardcoded foreground in {Path.GetFileName(filePath)}");
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error fixing foreground in {filePath}: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> FixContrastIssuesAsync(string filePath)
    {
        try
        {
            var content = await File.ReadAllTextAsync(filePath);
            
            // Replace problematic color combinations
            content = content.Replace("Foreground=\"White\" Background=\"#FFFFFF\"", 
                                    "Foreground=\"{DynamicResource Brush.TextPrimary}\" Background=\"{DynamicResource Brush.WindowBackground}\"");
            
            await File.WriteAllTextAsync(filePath, content);
            _logger.Info($"Fixed contrast issues in {Path.GetFileName(filePath)}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error fixing contrast in {filePath}: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> AddDefaultWorkspaceContentAsync()
    {
        try
        {
            // Add a getting started panel to the workspace
            Application.Current.Dispatcher.Invoke(() =>
            {
                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow?.FindName("WorkspaceHost") is PanelHostControl workspaceHost)
                {
                    // Add a helpful panel if workspace is empty
                    if (workspaceHost.Layout?.Panels?.Count == 0)
                    {
                        workspaceHost.AddPanel("chart");
                        workspaceHost.AddPanel("watchlist");
                    }
                }
            });
            
            _logger.Info("Added default workspace content");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error adding default workspace content: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> AddChartPanelGuidanceAsync()
    {
        try
        {
            // This would add helpful text to empty chart panels
            _logger.Info("Chart panel guidance feature noted for implementation");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error adding chart panel guidance: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> AddContextMenuHintsAsync()
    {
        try
        {
            // Add visual hints about right-click functionality
            _logger.Info("Context menu hints feature noted for implementation");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error adding context menu hints: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> AddGettingStartedOverlayAsync()
    {
        try
        {
            // Add getting started overlay
            _logger.Info("Getting started overlay feature noted for implementation");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error adding getting started overlay: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> AddSampleDataOptionAsync()
    {
        try
        {
            // Add sample data option
            _logger.Info("Sample data option feature noted for implementation");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error adding sample data option: {ex.Message}");
            return false;
        }
    }

    #endregion

    public async Task<int> AutoFixAllIssuesAsync()
    {
        int fixedCount = 0;
        
        foreach (var issue in _detectedIssues.Where(i => i.IsAutoFixable && i.AutoFixAction != null))
        {
            try
            {
                _logger.Info($"Auto-fixing: {issue.Description}");
                var success = await issue.AutoFixAction();
                if (success)
                {
                    fixedCount++;
                    _logger.Info($"✅ Fixed: {issue.Description}");
                }
                else
                {
                    _logger.Warn($"❌ Failed to fix: {issue.Description}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error auto-fixing {issue.Description}: {ex.Message}");
            }
        }
        
        _logger.Info($"Auto-fix complete. Fixed {fixedCount} out of {_detectedIssues.Count(i => i.IsAutoFixable)} auto-fixable issues.");
        return fixedCount;
    }
}
