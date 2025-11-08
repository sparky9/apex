using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace ApexV2.Extensions.Plugins
{
    // Plugin Lifecycle States
    public enum PluginStatus
    {
        Unloaded,
        Loading,
        Loaded,
        Running,
        Error,
        Disabled
    }

    // Plugin Types
    public enum PluginType
    {
        Indicator,
        DataProvider,
        UIPanel,
        Scanner,
        ExportFormat,
        Theme,
        Tool,
        Other
    }

    // Plugin Permission Levels
    [Flags]
    public enum PluginPermissions
    {
        None = 0,
        BasicOperations = 1,
        ReadMarketData = 2,
        WriteMarketData = 4,
        ReadUserData = 8,
        WriteUserData = 16,
        NetworkAccess = 32,
        FileSystemAccess = 64,
        DatabaseAccess = 128,
        UIAccess = 256,
        SystemAccess = 512,
        FullTrust = 1024,
        All = 2047
    }

    // Plugin Dependency Information
    public class PluginDependency
    {
        public string Name { get; set; } = string.Empty;
        public Version? MinVersion { get; set; }
        public Version? MaxVersion { get; set; }
        public bool IsOptional { get; set; }
        public string? Source { get; set; }
        public string? Description { get; set; }
    }

    // Plugin Metadata
    public class PluginInfo
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        [StringLength(500)]
        public string Description { get; set; } = string.Empty;
        
        [Required]
        public Version Version { get; set; } = new Version(1, 0, 0);
        
        [Required]
        [StringLength(100)]
        public string Author { get; set; } = string.Empty;
        
        [Url]
        public string? Website { get; set; }
        
        [EmailAddress]
        public string? ContactEmail { get; set; }
        
        public PluginType Type { get; set; }
        public PluginPermissions RequiredPermissions { get; set; }
        
        public Version MinimumApexVersion { get; set; } = new Version(2, 0, 0);
        public Version MaximumApexVersion { get; set; } = new Version(2, 99, 99);
        
        public List<PluginDependency> Dependencies { get; set; } = new();
        public List<string> Tags { get; set; } = new();
        
        public string? IconPath { get; set; }
        public string? LicenseType { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedDate { get; set; }
        
        // Missing properties needed by services
        public string Category { get; set; } = "Other";
        public string AssemblyPath { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public List<string> SupportedPlatforms { get; set; } = new();
        public bool IsEnabled { get; set; } = true;
        public DateTime InstallDate { get; set; } = DateTime.UtcNow;
        public DateTime LastModified { get; set; } = DateTime.UtcNow;
        
        public Dictionary<string, object> CustomProperties { get; set; } = new();
    }

    // Plugin Instance
    public class PluginInstance
    {
        public PluginInfo Info { get; set; } = new();
        public PluginStatus Status { get; set; } = PluginStatus.Unloaded;
        public IPlugin? Plugin { get; set; }
        public Assembly? Assembly { get; set; }
        public string? FilePath { get; set; }
        public DateTime LoadedAt { get; set; }
        public string? ErrorMessage { get; set; }
        public Exception? LastException { get; set; }
        public TimeSpan LoadTime { get; set; }
        public Dictionary<string, object> RuntimeData { get; set; } = new();
        public bool IsEnabled { get; set; } = true;
        public PluginPermissions GrantedPermissions { get; set; } = PluginPermissions.None;
        public PluginConfiguration Configuration { get; set; } = new();
    }

    // Plugin Configuration
    public class PluginConfiguration
    {
        public Guid PluginId { get; set; }
        public bool IsEnabled { get; set; } = true;
        public PluginPermissions GrantedPermissions { get; set; } = PluginPermissions.None;
        public Dictionary<string, object> Settings { get; set; } = new();
        public List<string> DisabledFeatures { get; set; } = new();
        public DateTime LastModified { get; set; } = DateTime.UtcNow;
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        public string? ModifiedBy { get; set; }
    }

    // Plugin Marketplace Entry
    public class PluginMarketplaceEntry
    {
        public Guid Id { get; set; }
        public PluginInfo PluginInfo { get; set; } = new();
        public string DownloadUrl { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string FileHash { get; set; } = string.Empty;
        public int DownloadCount { get; set; }
        public double Rating { get; set; }
        public int RatingCount { get; set; }
        public List<string> Screenshots { get; set; } = new();
        public string? ChangeLog { get; set; }
        public bool IsVerified { get; set; }
        public bool IsFeatured { get; set; }
        public DateTime PublishedDate { get; set; }
        public DateTime LastUpdated { get; set; }
        public decimal? Price { get; set; }
        public string? PriceCurrency { get; set; }
        public List<PluginReview> Reviews { get; set; } = new();
    }

    // Plugin Review
    public class PluginReview
    {
        public Guid Id { get; set; }
        public Guid PluginId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public int Rating { get; set; } // 1-5 stars
        public string? Title { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public Version PluginVersion { get; set; } = new Version(1, 0, 0);
        public bool IsVerified { get; set; }
        public List<string> HelpfulVotes { get; set; } = new();
    }

    // Plugin Event Arguments
    public class PluginEventArgs : EventArgs
    {
        public PluginInstance Plugin { get; }
        public string? Message { get; }
        public Exception? Exception { get; }

        public PluginEventArgs(PluginInstance plugin, string? message = null, Exception? exception = null)
        {
            Plugin = plugin;
            Message = message;
            Exception = exception;
        }
    }

    // Plugin Load Result
    public class PluginLoadResult
    {
        public bool Success { get; set; }
        public PluginInstance? Plugin { get; set; }
        public string? ErrorMessage { get; set; }
        public Exception? Exception { get; set; }
        public TimeSpan LoadTime { get; set; }
        public List<string> Warnings { get; set; } = new();
        public Dictionary<string, object> LoadMetadata { get; set; } = new();
    }

    // Plugin Validation Result
    public class PluginValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public PluginInfo? ExtractedInfo { get; set; }
        public List<string> RequiredAssemblies { get; set; } = new();
        public PluginPermissions DetectedPermissions { get; set; } = PluginPermissions.None;
        public List<string> SecurityIssues { get; set; } = new();
    }

    // Plugin Security Context
    public class PluginSecurityContext
    {
        public Guid PluginId { get; set; }
        public PluginPermissions GrantedPermissions { get; set; }
        public List<string> AllowedDirectories { get; set; } = new();
        public List<string> AllowedUrls { get; set; } = new();
        public bool IsSandboxed { get; set; } = true;
        public bool IsTrusted { get; set; } = false;
        public PluginSecurityLevel SecurityLevel { get; set; } = PluginSecurityLevel.Sandboxed;
        public TimeSpan ExecutionTimeout { get; set; } = TimeSpan.FromSeconds(30);
        public long MemoryLimit { get; set; } = 100 * 1024 * 1024; // 100MB
        public Dictionary<string, object> SecuritySettings { get; set; } = new();
    }

    // Plugin Performance Metrics
    public class PluginPerformanceMetrics
    {
        public Guid PluginId { get; set; }
        public TimeSpan TotalExecutionTime { get; set; }
        public TimeSpan AverageExecutionTime { get; set; }
        public long MemoryUsage { get; set; }
        public long PeakMemoryUsage { get; set; }
        public int CallCount { get; set; }
        public int ErrorCount { get; set; }
        public DateTime LastExecution { get; set; }
        public Dictionary<string, TimeSpan> MethodExecutionTimes { get; set; } = new();
    }

    // Plugin Update Information
    public class PluginUpdateInfo
    {
        public Guid PluginId { get; set; }
        public Version CurrentVersion { get; set; } = new Version(1, 0, 0);
        public Version LatestVersion { get; set; } = new Version(1, 0, 0);
        public bool UpdateAvailable { get; set; }
        public string? UpdateDescription { get; set; }
        public string? DownloadUrl { get; set; }
        public long UpdateSize { get; set; }
        public bool IsBreakingChange { get; set; }
        public bool IsSecurityUpdate { get; set; }
        public DateTime ReleaseDate { get; set; }
        public List<string> ChangeLog { get; set; } = new();
    }

    // Plugin Installation Package
    public class PluginPackage
    {
        public PluginInfo Info { get; set; } = new();
        public byte[] AssemblyData { get; set; } = Array.Empty<byte>();
        public Dictionary<string, byte[]> Resources { get; set; } = new();
        public string? ManifestJson { get; set; }
        public string PackageHash { get; set; } = string.Empty;
        public DateTime PackageDate { get; set; } = DateTime.UtcNow;
        public byte[]? Signature { get; set; }
        public string? SignatureAlgorithm { get; set; }
    }

    // Plugin State Enum (alias for PluginStatus)
    public enum PluginState
    {
        Unloaded = PluginStatus.Unloaded,
        Loading = PluginStatus.Loading,
        Loaded = PluginStatus.Loaded,
        Initializing,
        Running,
        Stopping,
        Unloading,
        Error = PluginStatus.Error,
        Disabled = PluginStatus.Disabled
    }

    // Plugin Security Manager
    public class PluginSecurityManager
    {
        public PluginPermissions DefaultPermissions { get; set; } = PluginPermissions.None;
        public List<string> TrustedPublishers { get; set; } = new List<string>();
        public List<string> BlockedPlugins { get; set; } = new List<string>();
        public bool RequireSignedPlugins { get; set; } = true;
        public bool AllowNetworkAccess { get; set; } = false;
        public bool AllowFileSystemAccess { get; set; } = false;
        public List<string> AllowedNetworkDomains { get; set; } = new List<string>();
        public List<string> AllowedDirectories { get; set; } = new List<string>();

        public PluginSecurityContext CreateSecurityContext(PluginInfo pluginInfo)
        {
            var grantedPermissions = CalculateGrantedPermissions(pluginInfo);
            return new PluginSecurityContext
            {
                PluginId = pluginInfo.Id,
                GrantedPermissions = grantedPermissions,
                AllowedUrls = GetAllowedUrls(pluginInfo),
                AllowedDirectories = GetAllowedDirectories(pluginInfo),
                IsTrusted = IsTrustedPlugin(pluginInfo),
                SecurityLevel = DetermineSecurityLevel(pluginInfo)
            };
        }

        private PluginPermissions CalculateGrantedPermissions(PluginInfo pluginInfo)
        {
            var granted = PluginPermissions.None;

            // Check requested permissions against policy
            if (pluginInfo.RequiredPermissions.HasFlag(PluginPermissions.ReadMarketData))
                granted |= PluginPermissions.ReadMarketData;

            if (pluginInfo.RequiredPermissions.HasFlag(PluginPermissions.UIAccess))
                granted |= PluginPermissions.UIAccess;

            if (AllowNetworkAccess && pluginInfo.RequiredPermissions.HasFlag(PluginPermissions.NetworkAccess))
                granted |= PluginPermissions.NetworkAccess;

            if (AllowFileSystemAccess && pluginInfo.RequiredPermissions.HasFlag(PluginPermissions.FileSystemAccess))
                granted |= PluginPermissions.FileSystemAccess;

            // Only grant dangerous permissions to trusted plugins
            if (IsTrustedPlugin(pluginInfo))
            {
                if (pluginInfo.RequiredPermissions.HasFlag(PluginPermissions.WriteMarketData))
                    granted |= PluginPermissions.WriteMarketData;

                if (pluginInfo.RequiredPermissions.HasFlag(PluginPermissions.DatabaseAccess))
                    granted |= PluginPermissions.DatabaseAccess;
            }

            return granted;
        }

        private bool IsTrustedPlugin(PluginInfo pluginInfo)
        {
            return TrustedPublishers.Contains(pluginInfo.Author, StringComparer.OrdinalIgnoreCase);
        }

        private List<string> GetAllowedUrls(PluginInfo pluginInfo)
        {
            return AllowedNetworkDomains.ToList();
        }

        private List<string> GetAllowedDirectories(PluginInfo pluginInfo)
        {
            return AllowedDirectories.ToList();
        }

        private PluginSecurityLevel DetermineSecurityLevel(PluginInfo pluginInfo)
        {
            if (IsTrustedPlugin(pluginInfo))
                return PluginSecurityLevel.Trusted;

            if (pluginInfo.RequiredPermissions.HasFlag(PluginPermissions.FullTrust))
                return PluginSecurityLevel.FullTrust;

            return PluginSecurityLevel.Sandboxed;
        }

        public void Dispose()
        {
            TrustedPublishers.Clear();
            BlockedPlugins.Clear();
            AllowedNetworkDomains.Clear();
            AllowedDirectories.Clear();
        }
    }

    // Plugin Security Level
    public enum PluginSecurityLevel
    {
        Sandboxed,
        Trusted,
        FullTrust
    }
}
