using Microsoft.EntityFrameworkCore;
using System.IO;

namespace ApexV2.Core.Database;

/// <summary>
/// Database service for managing SQLite database operations
/// </summary>
public class DatabaseService
{
    private readonly string _connectionString;
    private readonly string _databasePath;

    public DatabaseService(string? customPath = null)
    {
        // Default to user's AppData folder for database
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var apexDataPath = Path.Combine(appDataPath, "ApexV2");
        
        if (!Directory.Exists(apexDataPath))
        {
            Directory.CreateDirectory(apexDataPath);
        }

        _databasePath = customPath ?? Path.Combine(apexDataPath, "apex_v2.db");
        _connectionString = $"Data Source={_databasePath}";
    }

    /// <summary>
    /// Get configured DbContext options
    /// </summary>
    public virtual DbContextOptions<ApexDbContext> GetDbContextOptions()
    {
        return new DbContextOptionsBuilder<ApexDbContext>()
            .UseSqlite(_connectionString)
            .EnableSensitiveDataLogging(false) // Set to true for debugging
            .Options;
    }

    /// <summary>
    /// Initialize database - create if doesn't exist, migrate if needed
    /// </summary>
    public async Task<bool> InitializeDatabaseAsync()
    {
        try
        {
            using var context = new ApexDbContext(GetDbContextOptions());
            
            // Ensure database is created
            var created = await context.Database.EnsureCreatedAsync();
            
            if (created)
            {
                // Seed initial data
                await SeedInitialDataAsync(context);
            }

            return true;
        }
        catch (Exception ex)
        {
            // TODO [REVIEWED]: Add proper logging
            Console.WriteLine($"Database initialization failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Seed initial data for new database
    /// </summary>
    private async Task SeedInitialDataAsync(ApexDbContext context)
    {
        // Default user settings
        var defaultSettings = new[]
        {
            new UserSettingsEntity { Key = "Theme", Value = "Dark", Category = "UI", LastModified = DateTime.Now },
            new UserSettingsEntity { Key = "DefaultMarketDataProvider", Value = "YahooFinance", Category = "Data", LastModified = DateTime.Now },
            new UserSettingsEntity { Key = "DefaultTradingProvider", Value = "Alpaca", Category = "Trading", LastModified = DateTime.Now },
            new UserSettingsEntity { Key = "PaperTradingMode", Value = "true", Category = "Trading", LastModified = DateTime.Now },
            new UserSettingsEntity { Key = "AutoRefreshInterval", Value = "5000", Category = "Data", LastModified = DateTime.Now },
            new UserSettingsEntity { Key = "MaxPositionSize", Value = "1000", Category = "Safety", LastModified = DateTime.Now },
            new UserSettingsEntity { Key = "MaxDailyLoss", Value = "500", Category = "Safety", LastModified = DateTime.Now }
        };

        context.UserSettings.AddRange(defaultSettings);

        // Default watchlist
        var defaultWatchlist = new WatchlistEntity
        {
            Name = "Default Watchlist",
            Created = DateTime.Now,
            LastModified = DateTime.Now,
            Items = new List<WatchlistItemEntity>
            {
                new() { Symbol = "AAPL", SortOrder = 1, Added = DateTime.Now },
                new() { Symbol = "MSFT", SortOrder = 2, Added = DateTime.Now },
                new() { Symbol = "GOOGL", SortOrder = 3, Added = DateTime.Now },
                new() { Symbol = "SHOP.TO", SortOrder = 4, Added = DateTime.Now }, // Canadian stock
                new() { Symbol = "RY.TO", SortOrder = 5, Added = DateTime.Now }    // Royal Bank of Canada
            }
        };

        context.Watchlists.Add(defaultWatchlist);

        // Default workspace layout
        var defaultLayout = new WorkspaceLayoutEntity
        {
            Name = "Default Layout",
            LayoutData = """
            {
                "panels": [
                    {"id": "watchlist", "x": 0, "y": 0, "width": 300, "height": 400},
                    {"id": "chart", "x": 300, "y": 0, "width": 800, "height": 600},
                    {"id": "orders", "x": 0, "y": 400, "width": 300, "height": 200},
                    {"id": "positions", "x": 300, "y": 600, "width": 800, "height": 200}
                ]
            }
            """,
            IsDefault = true,
            Created = DateTime.Now,
            LastModified = DateTime.Now
        };

        context.WorkspaceLayouts.Add(defaultLayout);

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Test database connection
    /// </summary>
    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            using var context = new ApexDbContext(GetDbContextOptions());
            return await context.Database.CanConnectAsync();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get database file info
    /// </summary>
    public DatabaseInfo GetDatabaseInfo()
    {
        var fileInfo = new FileInfo(_databasePath);
        return new DatabaseInfo
        {
            Path = _databasePath,
            Exists = fileInfo.Exists,
            SizeBytes = fileInfo.Exists ? fileInfo.Length : 0,
            Created = fileInfo.Exists ? fileInfo.CreationTime : null,
            LastModified = fileInfo.Exists ? fileInfo.LastWriteTime : null
        };
    }

    /// <summary>
    /// Backup database to specified location
    /// </summary>
    public async Task<bool> BackupDatabaseAsync(string backupPath)
    {
        try
        {
            if (File.Exists(_databasePath))
            {
                await Task.Run(() => File.Copy(_databasePath, backupPath, true));
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Restore database from backup
    /// </summary>
    public async Task<bool> RestoreDatabaseAsync(string backupPath)
    {
        try
        {
            if (File.Exists(backupPath))
            {
                await Task.Run(() => File.Copy(backupPath, _databasePath, true));
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }
}

public class DatabaseInfo
{
    public string Path { get; set; } = string.Empty;
    public bool Exists { get; set; }
    public long SizeBytes { get; set; }
    public DateTime? Created { get; set; }
    public DateTime? LastModified { get; set; }
    
    public string SizeFormatted => FormatBytes(SizeBytes);
    
    private static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        return $"{number:n1} {suffixes[counter]}";
    }
}