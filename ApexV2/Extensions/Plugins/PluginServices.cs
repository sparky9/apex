using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ApexV2.Extensions.Plugins
{
    // Communication Hub for Plugin Message Passing
    public class PluginCommunicationHub
    {
        private readonly Dictionary<Guid, IPlugin> _plugins = new();
        private readonly Dictionary<string, Dictionary<Guid, Func<Dictionary<string, object>, Task>>> _subscriptions = new();
        private readonly ILogger<PluginCommunicationHub> _logger;

        public PluginCommunicationHub(ILogger<PluginCommunicationHub> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void RegisterPlugin(Guid pluginId, IPlugin plugin)
        {
            _plugins[pluginId] = plugin;
            _logger.LogDebug("Registered plugin {PluginId} for communication", pluginId);
        }

        public void UnregisterPlugin(Guid pluginId)
        {
            _plugins.Remove(pluginId);
            
            // Remove all subscriptions for this plugin
            foreach (var eventSubs in _subscriptions.Values)
            {
                eventSubs.Remove(pluginId);
            }
            
            _logger.LogDebug("Unregistered plugin {PluginId} from communication", pluginId);
        }

        public async Task<object?> SendMessageAsync(Guid fromPluginId, Guid toPluginId, string command, Dictionary<string, object> parameters)
        {
            if (!_plugins.TryGetValue(toPluginId, out var targetPlugin))
            {
                _logger.LogWarning("Target plugin {PluginId} not found for message from {FromPluginId}", toPluginId, fromPluginId);
                return null;
            }

            try
            {
                var messageArgs = new PluginMessageEventArgs(fromPluginId, command, parameters);

                MessageReceived?.Invoke(this, messageArgs);

                // For now, we'll just return success - in a real implementation,
                // plugins would handle messages through a standardized interface
                return "Message sent successfully";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message from {FromPluginId} to {ToPluginId}", fromPluginId, toPluginId);
                return null;
            }
        }

        public async Task BroadcastAsync(Guid fromPluginId, string command, Dictionary<string, object> parameters)
        {
            var tasks = _plugins.Where(p => p.Key != fromPluginId)
                .Select(p => SendMessageAsync(fromPluginId, p.Key, command, parameters));

            await Task.WhenAll(tasks);
        }

        public async Task SubscribeAsync(Guid pluginId, string eventName, Func<Dictionary<string, object>, Task> handler)
        {
            if (!_subscriptions.ContainsKey(eventName))
            {
                _subscriptions[eventName] = new Dictionary<Guid, Func<Dictionary<string, object>, Task>>();
            }

            _subscriptions[eventName][pluginId] = handler;
            await Task.CompletedTask;
        }

        public async Task UnsubscribeAsync(Guid pluginId, string eventName)
        {
            if (_subscriptions.TryGetValue(eventName, out var eventSubs))
            {
                eventSubs.Remove(pluginId);
            }
            await Task.CompletedTask;
        }

        public async Task PublishEventAsync(string eventName, Dictionary<string, object> eventData)
        {
            if (!_subscriptions.TryGetValue(eventName, out var eventSubs))
                return;

            var tasks = eventSubs.Values.Select(handler => 
                Task.Run(async () =>
                {
                    try
                    {
                        await handler(eventData);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in event handler for {EventName}", eventName);
                    }
                }));

            await Task.WhenAll(tasks);
        }

        public event EventHandler<PluginMessageEventArgs>? MessageReceived;

        public void Dispose()
        {
            _plugins.Clear();
            _subscriptions.Clear();
        }
    }

    // Plugin Discovery Service
    public class PluginDiscoveryService
    {
        private readonly ILogger<PluginDiscoveryService> _logger;
        private readonly string _pluginDirectory;

        public PluginDiscoveryService(ILogger<PluginDiscoveryService> logger, string pluginDirectory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _pluginDirectory = pluginDirectory ?? throw new ArgumentNullException(nameof(pluginDirectory));
        }

        public async Task<List<PluginInfo>> DiscoverPluginsAsync()
        {
            var plugins = new List<PluginInfo>();

            if (!Directory.Exists(_pluginDirectory))
            {
                _logger.LogWarning("Plugin directory does not exist: {PluginDirectory}", _pluginDirectory);
                return plugins;
            }

            var manifestFiles = Directory.GetFiles(_pluginDirectory, "plugin.json", SearchOption.AllDirectories);

            foreach (var manifestFile in manifestFiles)
            {
                try
                {
                    var plugin = await LoadPluginInfoAsync(manifestFile);
                    if (plugin != null)
                    {
                        plugins.Add(plugin);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading plugin manifest: {ManifestFile}", manifestFile);
                }
            }

            _logger.LogInformation("Discovered {PluginCount} plugins", plugins.Count);
            return plugins;
        }

        private async Task<PluginInfo?> LoadPluginInfoAsync(string manifestPath)
        {
            try
            {
                var json = await File.ReadAllTextAsync(manifestPath);
                var manifest = System.Text.Json.JsonSerializer.Deserialize<PluginManifest>(json);

                if (manifest == null)
                {
                    _logger.LogWarning("Invalid plugin manifest: {ManifestPath}", manifestPath);
                    return null;
                }

                var pluginInfo = new PluginInfo
                {
                    Id = manifest.Id,
                    Name = manifest.Name,
                    Version = manifest.Version,
                    Description = manifest.Description,
                    Author = manifest.Author,
                    Website = manifest.Website,
                    AssemblyPath = Path.Combine(Path.GetDirectoryName(manifestPath)!, manifest.AssemblyFile),
                    TypeName = manifest.TypeName,
                    RequiredPermissions = manifest.RequiredPermissions,
                    Dependencies = manifest.Dependencies?.ToList() ?? new List<PluginDependency>(),
                    SupportedPlatforms = manifest.SupportedPlatforms?.ToList() ?? new List<string>(),
                    MinimumApexVersion = manifest.MinimumApexVersion,
                    MaximumApexVersion = manifest.MaximumApexVersion,
                    Tags = manifest.Tags?.ToList() ?? new List<string>(),
                    Category = manifest.Category,
                    IsEnabled = false,
                    InstallDate = File.GetCreationTime(manifestPath),
                    LastModified = File.GetLastWriteTime(manifestPath)
                };

                return pluginInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing plugin manifest: {ManifestPath}", manifestPath);
                return null;
            }
        }

        public async Task<bool> ValidatePluginAsync(PluginInfo pluginInfo)
        {
            try
            {
                // Check if assembly file exists
                if (!File.Exists(pluginInfo.AssemblyPath))
                {
                    _logger.LogError("Plugin assembly not found: {AssemblyPath}", pluginInfo.AssemblyPath);
                    return false;
                }

                // Check platform compatibility
                var currentPlatform = Environment.OSVersion.Platform.ToString();
                if (pluginInfo.SupportedPlatforms.Any() && 
                    !pluginInfo.SupportedPlatforms.Contains(currentPlatform, StringComparer.OrdinalIgnoreCase))
                {
                    _logger.LogError("Plugin {PluginName} does not support current platform: {Platform}", 
                        pluginInfo.Name, currentPlatform);
                    return false;
                }

                // Check version compatibility
                var currentApexVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                if (pluginInfo.MinimumApexVersion != null && currentApexVersion < pluginInfo.MinimumApexVersion)
                {
                    _logger.LogError("Plugin {PluginName} requires minimum APEX version {MinVersion}, current: {CurrentVersion}", 
                        pluginInfo.Name, pluginInfo.MinimumApexVersion, currentApexVersion);
                    return false;
                }

                if (pluginInfo.MaximumApexVersion != null && currentApexVersion > pluginInfo.MaximumApexVersion)
                {
                    _logger.LogError("Plugin {PluginName} maximum supported APEX version {MaxVersion}, current: {CurrentVersion}", 
                        pluginInfo.Name, pluginInfo.MaximumApexVersion, currentApexVersion);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating plugin: {PluginName}", pluginInfo.Name);
                return false;
            }
        }
    }

    // Plugin Validation Service
    public class PluginValidationService
    {
        private readonly ILogger<PluginValidationService> _logger;

        public PluginValidationService(ILogger<PluginValidationService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<PluginValidationResult> ValidatePluginAsync(PluginInfo pluginInfo)
        {
            var result = new PluginValidationResult
            {
                IsValid = true,
                Errors = new List<string>(),
                Warnings = new List<string>(),
                SecurityIssues = new List<string>()
            };

            try
            {
                // Basic validation
                await ValidateBasicInfo(pluginInfo, result);
                
                // Assembly validation
                await ValidateAssembly(pluginInfo, result);
                
                // Security validation
                await ValidateSecurityRequirements(pluginInfo, result);
                
                // Dependency validation
                await ValidateDependencies(pluginInfo, result);

                result.IsValid = result.Errors.Count == 0 && result.SecurityIssues.Count == 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during plugin validation: {PluginName}", pluginInfo.Name);
                result.IsValid = false;
                result.Errors.Add($"Validation failed with exception: {ex.Message}");
            }

            return result;
        }

        private async Task ValidateBasicInfo(PluginInfo pluginInfo, PluginValidationResult result)
        {
            if (string.IsNullOrWhiteSpace(pluginInfo.Name))
                result.Errors.Add("Plugin name is required");

            if (pluginInfo.Version == null)
                result.Errors.Add("Plugin version is required");

            if (string.IsNullOrWhiteSpace(pluginInfo.Author))
                result.Warnings.Add("Plugin author is not specified");

            if (string.IsNullOrWhiteSpace(pluginInfo.Description))
                result.Warnings.Add("Plugin description is not provided");

            await Task.CompletedTask;
        }

        private async Task ValidateAssembly(PluginInfo pluginInfo, PluginValidationResult result)
        {
            if (!File.Exists(pluginInfo.AssemblyPath))
            {
                result.Errors.Add($"Assembly file not found: {pluginInfo.AssemblyPath}");
                return;
            }

            try
            {
                // Try to load assembly metadata without loading it into the current domain
                var assemblyName = System.Reflection.AssemblyName.GetAssemblyName(pluginInfo.AssemblyPath);
                
                if (string.IsNullOrWhiteSpace(pluginInfo.TypeName))
                {
                    result.Errors.Add("Plugin type name is required");
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Invalid assembly: {ex.Message}");
            }

            await Task.CompletedTask;
        }

        private async Task ValidateSecurityRequirements(PluginInfo pluginInfo, PluginValidationResult result)
        {
            // Check for dangerous permission combinations
            if (pluginInfo.RequiredPermissions.HasFlag(PluginPermissions.FileSystemAccess) && 
                pluginInfo.RequiredPermissions.HasFlag(PluginPermissions.NetworkAccess))
            {
                result.SecurityIssues.Add("Plugin requests both file system and network access - review carefully");
            }

            if (pluginInfo.RequiredPermissions.HasFlag(PluginPermissions.DatabaseAccess))
            {
                result.SecurityIssues.Add("Plugin requests database access - ensure data safety measures");
            }

            if (pluginInfo.RequiredPermissions.HasFlag(PluginPermissions.FullTrust))
            {
                result.SecurityIssues.Add("Plugin requests full trust - extremely dangerous, review thoroughly");
            }

            await Task.CompletedTask;
        }

        private async Task ValidateDependencies(PluginInfo pluginInfo, PluginValidationResult result)
        {
            foreach (var dependency in pluginInfo.Dependencies)
            {
                // For now, just validate that dependency information is complete
                if (string.IsNullOrWhiteSpace(dependency.Name))
                {
                    result.Errors.Add("Dependency name is required");
                }

                if (dependency.MinVersion == null)
                {
                    result.Warnings.Add($"Minimum version not specified for dependency: {dependency.Name}");
                }
            }

            await Task.CompletedTask;
        }
    }

    // Plugin Manifest for JSON serialization
    public class PluginManifest
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public Version Version { get; set; } = new Version(1, 0, 0);
        public string Description { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string? Website { get; set; }
        public string AssemblyFile { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public PluginPermissions RequiredPermissions { get; set; }
        public PluginDependency[]? Dependencies { get; set; }
        public string[]? SupportedPlatforms { get; set; }
        public Version? MinimumApexVersion { get; set; }
        public Version? MaximumApexVersion { get; set; }
        public string[]? Tags { get; set; }
        public string Category { get; set; } = "Other";
    }
}
