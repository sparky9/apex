using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text.Json;

namespace ApexV2.Extensions.Plugins
{
    public class PluginManager : IPluginManager, IDisposable
    {
        private readonly ILogger<PluginManager> _logger;
        private readonly ConcurrentDictionary<Guid, PluginInstance> _loadedPlugins;
        private readonly ConcurrentDictionary<Guid, PluginConfiguration> _pluginConfigurations;
        private readonly string _pluginDirectory;
        private readonly string _configDirectory;
        private readonly PluginSecurityManager _securityManager;
        private readonly PluginCommunicationHub _communicationHub;
        private bool _disposed;

        public PluginManager(ILogger<PluginManager> logger, string pluginDirectory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _pluginDirectory = pluginDirectory ?? throw new ArgumentNullException(nameof(pluginDirectory));
            _configDirectory = Path.Combine(_pluginDirectory, "config");
            
            _loadedPlugins = new ConcurrentDictionary<Guid, PluginInstance>();
            _pluginConfigurations = new ConcurrentDictionary<Guid, PluginConfiguration>();
            _securityManager = new PluginSecurityManager();
            
            // Create logger for communication hub
            var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole());
            var commHubLogger = loggerFactory.CreateLogger<PluginCommunicationHub>();
            _communicationHub = new PluginCommunicationHub(commHubLogger);

            // Ensure directories exist
            Directory.CreateDirectory(_pluginDirectory);
            Directory.CreateDirectory(_configDirectory);

            _logger.LogInformation("Plugin Manager initialized with directory: {PluginDirectory}", _pluginDirectory);
        }

        #region Plugin Lifecycle

        public async Task<PluginLoadResult> LoadPluginAsync(string filePath)
        {
            var startTime = DateTime.UtcNow;
            var result = new PluginLoadResult();

            try
            {
                _logger.LogInformation("Loading plugin from: {FilePath}", filePath);

                // Validate plugin file
                var validationResult = await ValidatePluginAsync(filePath);
                if (!validationResult.IsValid)
                {
                    result.ErrorMessage = $"Plugin validation failed: {string.Join(", ", validationResult.Errors)}";
                    result.Warnings.AddRange(validationResult.Warnings);
                    _logger.LogWarning("Plugin validation failed for {FilePath}: {Errors}", filePath, result.ErrorMessage);
                    return result;
                }

                // Load assembly
                var assemblyData = await File.ReadAllBytesAsync(filePath);
                var assembly = Assembly.Load(assemblyData);

                // Find plugin implementation
                var pluginType = FindPluginType(assembly);
                if (pluginType == null)
                {
                    result.ErrorMessage = "No plugin implementation found in assembly";
                    _logger.LogError("No plugin implementation found in {FilePath}", filePath);
                    return result;
                }

                // Create plugin instance
                var plugin = (IPlugin)Activator.CreateInstance(pluginType)!;
                var pluginInfo = plugin.Info;

                // Check if plugin is already loaded
                if (_loadedPlugins.Values.Any(p => p.Info.Id == pluginInfo.Id))
                {
                    result.ErrorMessage = $"Plugin {pluginInfo.Name} is already loaded";
                    _logger.LogWarning("Plugin {PluginName} is already loaded", pluginInfo.Name);
                    return result;
                }

                // Create plugin instance wrapper
                var pluginInstance = new PluginInstance
                {
                    Info = pluginInfo,
                    Plugin = plugin,
                    Assembly = assembly,
                    FilePath = filePath,
                    Status = PluginStatus.Loading,
                    LoadedAt = DateTime.UtcNow
                };

                // Load configuration
                var configuration = await LoadPluginConfigurationAsync(pluginInfo.Id);
                _pluginConfigurations[pluginInfo.Id] = configuration;

                // Create security context
                var securityContext = _securityManager.CreateSecurityContext(pluginInfo);
                pluginInstance.GrantedPermissions = securityContext.GrantedPermissions;

                // Create plugin context
                var pluginContext = new PluginContext(
                    pluginInfo,
                    securityContext,
                    _logger,
                    configuration,
                    _communicationHub
                );

                // Initialize plugin
                var initResult = await plugin.InitializeAsync(pluginContext);
                if (!initResult)
                {
                    result.ErrorMessage = "Plugin initialization failed";
                    _logger.LogError("Plugin {PluginName} initialization failed", pluginInfo.Name);
                    return result;
                }

                // Start plugin
                var startResult = await plugin.StartAsync();
                if (!startResult)
                {
                    result.ErrorMessage = "Plugin start failed";
                    _logger.LogError("Plugin {PluginName} start failed", pluginInfo.Name);
                    return result;
                }

                // Update status and add to loaded plugins
                pluginInstance.Status = PluginStatus.Loaded;
                pluginInstance.LoadTime = DateTime.UtcNow - startTime;
                _loadedPlugins[pluginInfo.Id] = pluginInstance;

                // Subscribe to plugin events
                plugin.StatusChanged += OnPluginStatusChanged;
                plugin.ErrorOccurred += OnPluginErrorOccurred;

                result.Success = true;
                result.Plugin = pluginInstance;
                result.LoadTime = pluginInstance.LoadTime;

                _logger.LogInformation("Plugin {PluginName} loaded successfully in {LoadTime}ms", 
                    pluginInfo.Name, pluginInstance.LoadTime.TotalMilliseconds);

                PluginLoaded?.Invoke(this, new PluginEventArgs(pluginInstance, "Plugin loaded successfully"));
            }
            catch (Exception ex)
            {
                result.Exception = ex;
                result.ErrorMessage = ex.Message;
                _logger.LogError(ex, "Error loading plugin from {FilePath}", filePath);
            }

            result.LoadTime = DateTime.UtcNow - startTime;
            return result;
        }

        public async Task<bool> UnloadPluginAsync(Guid pluginId)
        {
            try
            {
                if (!_loadedPlugins.TryGetValue(pluginId, out var pluginInstance))
                {
                    _logger.LogWarning("Plugin {PluginId} not found for unloading", pluginId);
                    return false;
                }

                _logger.LogInformation("Unloading plugin {PluginName}", pluginInstance.Info.Name);

                // Update status
                pluginInstance.Status = PluginStatus.Unloaded;

                // Stop plugin
                if (pluginInstance.Plugin != null)
                {
                    await pluginInstance.Plugin.StopAsync();
                    await pluginInstance.Plugin.ShutdownAsync();

                    // Unsubscribe from events
                    pluginInstance.Plugin.StatusChanged -= OnPluginStatusChanged;
                    pluginInstance.Plugin.ErrorOccurred -= OnPluginErrorOccurred;

                    // Dispose plugin
                    pluginInstance.Plugin.Dispose();
                }

                // Remove from loaded plugins
                _loadedPlugins.TryRemove(pluginId, out _);

                _logger.LogInformation("Plugin {PluginName} unloaded successfully", pluginInstance.Info.Name);

                PluginUnloaded?.Invoke(this, new PluginEventArgs(pluginInstance, "Plugin unloaded successfully"));
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unloading plugin {PluginId}", pluginId);
                return false;
            }
        }

        public async Task<bool> EnablePluginAsync(Guid pluginId)
        {
            try
            {
                if (!_loadedPlugins.TryGetValue(pluginId, out var pluginInstance))
                {
                    _logger.LogWarning("Plugin {PluginId} not found for enabling", pluginId);
                    return false;
                }

                if (!pluginInstance.IsEnabled)
                {
                    pluginInstance.IsEnabled = true;
                    if (pluginInstance.Plugin != null)
                    {
                        await pluginInstance.Plugin.StartAsync();
                    }

                    // Update configuration
                    if (_pluginConfigurations.TryGetValue(pluginId, out var config))
                    {
                        config.IsEnabled = true;
                        await SavePluginConfigurationAsync(config);
                    }

                    _logger.LogInformation("Plugin {PluginName} enabled", pluginInstance.Info.Name);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enabling plugin {PluginId}", pluginId);
                return false;
            }
        }

        public async Task<bool> DisablePluginAsync(Guid pluginId)
        {
            try
            {
                if (!_loadedPlugins.TryGetValue(pluginId, out var pluginInstance))
                {
                    _logger.LogWarning("Plugin {PluginId} not found for disabling", pluginId);
                    return false;
                }

                if (pluginInstance.IsEnabled)
                {
                    pluginInstance.IsEnabled = false;
                    if (pluginInstance.Plugin != null)
                    {
                        await pluginInstance.Plugin.StopAsync();
                    }

                    // Update configuration
                    if (_pluginConfigurations.TryGetValue(pluginId, out var config))
                    {
                        config.IsEnabled = false;
                        await SavePluginConfigurationAsync(config);
                    }

                    _logger.LogInformation("Plugin {PluginName} disabled", pluginInstance.Info.Name);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disabling plugin {PluginId}", pluginId);
                return false;
            }
        }

        public async Task<bool> ReloadPluginAsync(Guid pluginId)
        {
            try
            {
                if (!_loadedPlugins.TryGetValue(pluginId, out var pluginInstance))
                {
                    _logger.LogWarning("Plugin {PluginId} not found for reloading", pluginId);
                    return false;
                }

                var filePath = pluginInstance.FilePath;
                if (string.IsNullOrEmpty(filePath))
                {
                    _logger.LogError("Plugin {PluginName} has no file path for reloading", pluginInstance.Info.Name);
                    return false;
                }

                // Unload current instance
                await UnloadPluginAsync(pluginId);

                // Load new instance
                var loadResult = await LoadPluginAsync(filePath);
                return loadResult.Success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reloading plugin {PluginId}", pluginId);
                return false;
            }
        }

        #endregion

        #region Plugin Information

        public IReadOnlyList<PluginInstance> GetLoadedPlugins()
        {
            return _loadedPlugins.Values.ToList().AsReadOnly();
        }

        public PluginInstance? GetPlugin(Guid pluginId)
        {
            _loadedPlugins.TryGetValue(pluginId, out var plugin);
            return plugin;
        }

        public IReadOnlyList<PluginInstance> GetPluginsByType(PluginType type)
        {
            return _loadedPlugins.Values
                .Where(p => p.Info.Type == type)
                .ToList()
                .AsReadOnly();
        }

        public IReadOnlyList<PluginInstance> GetRunningPlugins()
        {
            return _loadedPlugins.Values
                .Where(p => p.Status == PluginStatus.Running)
                .ToList()
                .AsReadOnly();
        }

        public IReadOnlyList<PluginInstance> GetAllPlugins()
        {
            return GetLoadedPlugins();
        }

        public async Task<PluginConfiguration?> GetPluginConfigurationAsync(Guid pluginId)
        {
            try
            {
                if (_pluginConfigurations.TryGetValue(pluginId, out var config))
                {
                    return config;
                }

                if (_loadedPlugins.TryGetValue(pluginId, out var plugin))
                {
                    return plugin.Configuration;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get configuration for plugin {PluginId}", pluginId);
                return null;
            }
        }

        public async Task<bool> StopPluginAsync(Guid pluginId)
        {
            if (!_loadedPlugins.TryGetValue(pluginId, out var pluginInstance))
            {
                _logger.LogWarning("Plugin {PluginId} not found for stopping", pluginId);
                return false;
            }

            try
            {
                if (pluginInstance.Status != PluginStatus.Running)
                {
                    _logger.LogInformation("Plugin {PluginId} is not running", pluginId);
                    return true;
                }

                // Stop the plugin (if it has a stop method)
                pluginInstance.Status = PluginStatus.Loaded;
                _logger.LogInformation("Stopped plugin {PluginName} ({PluginId})", pluginInstance.Info.Name, pluginId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop plugin {PluginId}", pluginId);
                pluginInstance.Status = PluginStatus.Error;
                pluginInstance.ErrorMessage = ex.Message;
                pluginInstance.LastException = ex;
                return false;
            }
        }

        public async Task<bool> StartPluginAsync(Guid pluginId)
        {
            if (!_loadedPlugins.TryGetValue(pluginId, out var pluginInstance))
            {
                _logger.LogWarning("Plugin {PluginId} not found for starting", pluginId);
                return false;
            }

            try
            {
                if (pluginInstance.Status == PluginStatus.Running)
                {
                    _logger.LogInformation("Plugin {PluginId} is already running", pluginId);
                    return true;
                }

                if (pluginInstance.Plugin != null)
                {
                    // Start the plugin (if it has a start method)
                    pluginInstance.Status = PluginStatus.Running;
                    _logger.LogInformation("Started plugin {PluginName} ({PluginId})", pluginInstance.Info.Name, pluginId);
                    return true;
                }

                _logger.LogError("Plugin {PluginId} has no plugin instance to start", pluginId);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start plugin {PluginId}", pluginId);
                pluginInstance.Status = PluginStatus.Error;
                pluginInstance.ErrorMessage = ex.Message;
                pluginInstance.LastException = ex;
                return false;
            }
        }

        public async Task StartAllEnabledPluginsAsync()
        {
            var enabledPlugins = _loadedPlugins.Values
                .Where(p => p.Status == PluginStatus.Loaded && p.Configuration.IsEnabled)
                .ToList();

            foreach (var plugin in enabledPlugins)
            {
                try
                {
                    await StartPluginAsync(plugin.Info.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to start plugin {PluginId}", plugin.Info.Id);
                }
            }
        }

        public async Task InitializeAsync()
        {
            _logger.LogInformation("Initializing Plugin Manager");
            
            // Load existing plugin configurations
            await LoadPluginConfigurationsAsync();
            
            // Discover plugins in the plugin directory
            var pluginFiles = await DiscoverPluginsAsync(_pluginDirectory);
            _logger.LogInformation("Discovered {Count} plugin files", pluginFiles.Count);
            
            await Task.CompletedTask;
        }

        public async Task LoadAllPluginsAsync()
        {
            _logger.LogInformation("Loading all plugins");
            
            var pluginFiles = await DiscoverPluginsAsync(_pluginDirectory);
            
            foreach (var pluginFile in pluginFiles)
            {
                try
                {
                    var result = await LoadPluginAsync(pluginFile);
                    if (result.Success)
                    {
                        _logger.LogInformation("Successfully loaded plugin: {PluginName}", result.Plugin?.Info.Name);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to load plugin from {FilePath}: {Error}", pluginFile, result.ErrorMessage);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading plugin from {FilePath}", pluginFile);
                }
            }
        }

        public async Task<bool> ResetPluginConfigurationAsync(Guid pluginId)
        {
            try
            {
                if (_loadedPlugins.TryGetValue(pluginId, out var plugin))
                {
                    // Create default configuration
                    var defaultConfig = new PluginConfiguration
                    {
                        PluginId = pluginId,
                        IsEnabled = true,
                        Settings = new Dictionary<string, object>(),
                        LastUpdated = DateTime.UtcNow
                    };

                    // Update configuration
                    plugin.Configuration = defaultConfig;
                    _pluginConfigurations[pluginId] = defaultConfig;

                    // Save configuration
                    await SavePluginConfigurationAsync(pluginId, defaultConfig);

                    _logger.LogInformation("Reset configuration for plugin {PluginId}", pluginId);
                    return true;
                }

                _logger.LogWarning("Plugin {PluginId} not found for configuration reset", pluginId);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reset configuration for plugin {PluginId}", pluginId);
                return false;
            }
        }

        private async Task LoadPluginConfigurationsAsync()
        {
            try
            {
                var configFiles = Directory.GetFiles(_configDirectory, "*.json");
                foreach (var configFile in configFiles)
                {
                    var json = await File.ReadAllTextAsync(configFile);
                    var config = JsonSerializer.Deserialize<PluginConfiguration>(json);
                    if (config != null)
                    {
                        _pluginConfigurations[config.PluginId] = config;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load plugin configurations");
            }
        }

        private async Task SavePluginConfigurationAsync(Guid pluginId, PluginConfiguration configuration)
        {
            try
            {
                var configPath = Path.Combine(_configDirectory, $"{pluginId}.json");
                var json = JsonSerializer.Serialize(configuration, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(configPath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save configuration for plugin {PluginId}", pluginId);
            }
        }

        #endregion

        #region Plugin Discovery

        public async Task<List<string>> DiscoverPluginsAsync(string directory)
        {
            try
            {
                if (!Directory.Exists(directory))
                {
                    _logger.LogWarning("Plugin directory does not exist: {Directory}", directory);
                    return new List<string>();
                }

                var pluginFiles = new List<string>();
                var dllFiles = Directory.GetFiles(directory, "*.dll", SearchOption.AllDirectories);

                foreach (var file in dllFiles)
                {
                    try
                    {
                        var validationResult = await ValidatePluginAsync(file);
                        if (validationResult.IsValid)
                        {
                            pluginFiles.Add(file);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "File {File} is not a valid plugin", file);
                    }
                }

                _logger.LogInformation("Discovered {Count} plugin files in {Directory}", pluginFiles.Count, directory);
                return pluginFiles;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error discovering plugins in {Directory}", directory);
                return new List<string>();
            }
        }

        public async Task<PluginValidationResult> ValidatePluginAsync(string filePath)
        {
            var result = new PluginValidationResult();

            try
            {
                if (!File.Exists(filePath))
                {
                    result.Errors.Add("Plugin file does not exist");
                    return result;
                }

                // Load assembly for validation
                var assemblyData = await File.ReadAllBytesAsync(filePath);
                var assembly = Assembly.Load(assemblyData);

                // Find plugin type
                var pluginType = FindPluginType(assembly);
                if (pluginType == null)
                {
                    result.Errors.Add("No plugin implementation found");
                    return result;
                }

                // Create temporary instance to get info
                var tempPlugin = (IPlugin)Activator.CreateInstance(pluginType)!;
                result.ExtractedInfo = tempPlugin.Info;
                tempPlugin.Dispose();

                // Validate plugin info
                if (string.IsNullOrWhiteSpace(result.ExtractedInfo.Name))
                {
                    result.Errors.Add("Plugin name is required");
                }

                if (string.IsNullOrWhiteSpace(result.ExtractedInfo.Author))
                {
                    result.Errors.Add("Plugin author is required");
                }

                if (result.ExtractedInfo.Version == null)
                {
                    result.Errors.Add("Plugin version is required");
                }

                // Check version compatibility
                var currentApexVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(2, 0, 0);
                if (result.ExtractedInfo.MinimumApexVersion > currentApexVersion)
                {
                    result.Errors.Add($"Plugin requires APEX version {result.ExtractedInfo.MinimumApexVersion} or higher");
                }

                if (result.ExtractedInfo.MaximumApexVersion < currentApexVersion)
                {
                    result.Warnings.Add($"Plugin may not be compatible with current APEX version {currentApexVersion}");
                }

                // Analyze permissions
                result.DetectedPermissions = AnalyzePermissions(assembly);

                result.IsValid = result.Errors.Count == 0;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Validation error: {ex.Message}");
                _logger.LogError(ex, "Error validating plugin {FilePath}", filePath);
            }

            return result;
        }

        #endregion

        #region Plugin Configuration

        public async Task<bool> UpdatePluginConfigurationAsync(Guid pluginId, PluginConfiguration configuration)
        {
            try
            {
                configuration.PluginId = pluginId;
                configuration.LastModified = DateTime.UtcNow;
                
                _pluginConfigurations[pluginId] = configuration;
                await SavePluginConfigurationAsync(configuration);

                // Apply configuration to loaded plugin
                if (_loadedPlugins.TryGetValue(pluginId, out var pluginInstance) && pluginInstance.Plugin != null)
                {
                    await pluginInstance.Plugin.ConfigureAsync(configuration.Settings);
                }

                _logger.LogInformation("Configuration updated for plugin {PluginId}", pluginId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating configuration for plugin {PluginId}", pluginId);
                return false;
            }
        }

        public PluginConfiguration? GetPluginConfiguration(Guid pluginId)
        {
            _pluginConfigurations.TryGetValue(pluginId, out var configuration);
            return configuration;
        }

        #endregion

        #region Plugin Communication

        public async Task<object?> SendMessageToPluginAsync(Guid pluginId, string command, Dictionary<string, object>? parameters = null)
        {
            try
            {
                if (!_loadedPlugins.TryGetValue(pluginId, out var pluginInstance) || 
                    pluginInstance.Plugin == null || 
                    !pluginInstance.IsEnabled)
                {
                    _logger.LogWarning("Plugin {PluginId} not found or not enabled for message", pluginId);
                    return null;
                }

                return await pluginInstance.Plugin.ExecuteAsync(command, parameters);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message to plugin {PluginId}", pluginId);
                return null;
            }
        }

        public async Task BroadcastMessageAsync(string command, Dictionary<string, object>? parameters = null)
        {
            var tasks = _loadedPlugins.Values
                .Where(p => p.Plugin != null && p.IsEnabled)
                .Select(p => SendMessageToPluginAsync(p.Info.Id, command, parameters));

            await Task.WhenAll(tasks);
        }

        #endregion

        #region Private Methods

        private Type? FindPluginType(Assembly assembly)
        {
            return assembly.GetTypes()
                .FirstOrDefault(t => t.IsClass && !t.IsAbstract && typeof(IPlugin).IsAssignableFrom(t));
        }

        private PluginPermissions AnalyzePermissions(Assembly assembly)
        {
            var permissions = PluginPermissions.None;

            // Simple analysis based on referenced types/namespaces
            var types = assembly.GetTypes();
            var referencedTypes = types.SelectMany(t => t.GetMembers()).Select(m => m.DeclaringType?.FullName).Distinct();

            foreach (var typeName in referencedTypes.Where(t => t != null))
            {
                if (typeName!.StartsWith("System.Net"))
                    permissions |= PluginPermissions.NetworkAccess;
                if (typeName.StartsWith("System.IO"))
                    permissions |= PluginPermissions.FileSystemAccess;
                if (typeName.StartsWith("System.Data") || typeName.Contains("Database"))
                    permissions |= PluginPermissions.DatabaseAccess;
            }

            return permissions;
        }

        private async Task<PluginConfiguration> LoadPluginConfigurationAsync(Guid pluginId)
        {
            try
            {
                var configPath = Path.Combine(_configDirectory, $"{pluginId}.json");
                if (File.Exists(configPath))
                {
                    var json = await File.ReadAllTextAsync(configPath);
                    var config = JsonSerializer.Deserialize<PluginConfiguration>(json);
                    if (config != null)
                    {
                        return config;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error loading configuration for plugin {PluginId}", pluginId);
            }

            return new PluginConfiguration { PluginId = pluginId };
        }

        private async Task SavePluginConfigurationAsync(PluginConfiguration configuration)
        {
            try
            {
                var configPath = Path.Combine(_configDirectory, $"{configuration.PluginId}.json");
                var json = JsonSerializer.Serialize(configuration, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(configPath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving configuration for plugin {PluginId}", configuration.PluginId);
            }
        }

        private void OnPluginStatusChanged(object? sender, PluginEventArgs e)
        {
            PluginStatusChanged?.Invoke(this, e);
        }

        private void OnPluginErrorOccurred(object? sender, PluginEventArgs e)
        {
            if (e.Plugin != null)
            {
                e.Plugin.Status = PluginStatus.Error;
                e.Plugin.ErrorMessage = e.Message;
                e.Plugin.LastException = e.Exception;
            }

            PluginError?.Invoke(this, e);
        }

        #endregion

        #region Events

        public event EventHandler<PluginEventArgs>? PluginLoaded;
        public event EventHandler<PluginEventArgs>? PluginUnloaded;
        public event EventHandler<PluginEventArgs>? PluginError;
        public event EventHandler<PluginEventArgs>? PluginStatusChanged;

        #endregion

        #region IDisposable

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Unload all plugins
                    var unloadTasks = _loadedPlugins.Keys.Select(UnloadPluginAsync);
                    Task.WhenAll(unloadTasks).GetAwaiter().GetResult();

                    _securityManager?.Dispose();
                    _communicationHub?.Dispose();
                }

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
