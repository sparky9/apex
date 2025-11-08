using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ApexV2.Validation
{
    /// <summary>
    /// Simple UX validation system for detecting and auto-fixing UI/UX issues
    /// </summary>
    public class UXValidationSystemSimple
    {
        private readonly string _projectPath;
        private readonly List<UXIssue> _detectedIssues = new List<UXIssue>();

        public UXValidationSystemSimple(string projectPath)
        {
            _projectPath = projectPath;
        }

        public async Task<List<UXIssue>> ValidateUserExperienceAsync()
        {
            _detectedIssues.Clear();
            
            Console.WriteLine("🎨 Validating UI/UX...");
            
            await ValidateXamlContrast();
            await ValidatePlaceholderMessages();
            
            return _detectedIssues.ToList();
        }

        private async Task ValidateXamlContrast()
        {
            var xamlFiles = Directory.GetFiles(_projectPath, "*.xaml", SearchOption.AllDirectories)
                .Where(f => !f.Contains("\\bin\\") && !f.Contains("\\obj\\"))
                .ToArray();

            foreach (var file in xamlFiles)
            {
                try
                {
                    var content = await File.ReadAllTextAsync(file);
                    var relativePath = Path.GetRelativePath(_projectPath, file);

                    // Check for hardcoded white foreground (basic check)
                    if (content.Contains("Foreground=\"White\""))
                    {
                        _detectedIssues.Add(new UXIssue
                        {
                            Category = "Color Contrast",
                            Severity = "High", 
                            Description = "Hardcoded white foreground may cause contrast issues",
                            Location = relativePath,
                            Recommendation = "Use theme-aware color resources instead of hardcoded colors",
                            IsAutoFixable = true
                        });
                    }
                }
                catch (Exception ex)
                {
                    _detectedIssues.Add(new UXIssue
                    {
                        Category = "File Access",
                        Severity = "Low",
                        Description = $"Could not analyze XAML file: {ex.Message}",
                        Location = file,
                        Recommendation = "Check file accessibility",
                        IsAutoFixable = false
                    });
                }
            }
        }

        private async Task ValidatePlaceholderMessages()
        {
            var csFiles = Directory.GetFiles(_projectPath, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains("\\bin\\") && !f.Contains("\\obj\\"))
                .ToArray();

            foreach (var file in csFiles)
            {
                try
                {
                    var content = await File.ReadAllTextAsync(file);
                    var relativePath = Path.GetRelativePath(_projectPath, file);

                    // Check for "Coming Soon" messages
                    if (content.Contains("Coming Soon"))
                    {
                        _detectedIssues.Add(new UXIssue
                        {
                            Category = "Placeholder Messages",
                            Severity = "Medium",
                            Description = "Contains 'Coming Soon' placeholder message",
                            Location = relativePath,
                            Recommendation = "Replace placeholder with actual functionality",
                            IsAutoFixable = false
                        });
                    }
                }
                catch (Exception ex)
                {
                    _detectedIssues.Add(new UXIssue
                    {
                        Category = "File Access",
                        Severity = "Low",
                        Description = $"Could not analyze CS file: {ex.Message}",
                        Location = file,
                        Recommendation = "Check file accessibility",
                        IsAutoFixable = false
                    });
                }
            }
        }

        public async Task<int> AutoFixIssuesAsync(List<UXIssue> issues)
        {
            var fixedCount = 0;
            
            foreach (var issue in issues.Where(i => i.IsAutoFixable))
            {
                try
                {
                    if (issue.Category == "Color Contrast" && issue.Description.Contains("white foreground"))
                    {
                        await FixHardcodedWhiteForeground(issue.Location);
                        fixedCount++;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to auto-fix issue in {issue.Location}: {ex.Message}");
                }
            }
            
            return fixedCount;
        }

        private async Task FixHardcodedWhiteForeground(string filePath)
        {
            var fullPath = Path.Combine(_projectPath, filePath);
            if (!File.Exists(fullPath)) return;
            
            var content = await File.ReadAllTextAsync(fullPath);
            var originalContent = content;
            
            // Replace hardcoded white foregrounds
            content = content.Replace("Foreground=\"White\"", "");
            
            if (content != originalContent)
            {
                await File.WriteAllTextAsync(fullPath, content);
                Console.WriteLine($"🔧 Fixed hardcoded white foreground in {filePath}");
            }
        }
    }

    public class UXIssue
    {
        public string Category { get; set; } = "";
        public string Severity { get; set; } = "";
        public string Description { get; set; } = "";
        public string Location { get; set; } = "";
        public string Recommendation { get; set; } = "";
        public bool IsAutoFixable { get; set; } = false;
    }
}
