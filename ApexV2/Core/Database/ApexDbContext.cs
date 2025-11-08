using Microsoft.EntityFrameworkCore;
using ApexV2.Data.MarketData;
using ApexV2.Data.Trading;
using System.ComponentModel.DataAnnotations; // added

namespace ApexV2.Core.Database;

/// <summary>
/// Main database context for APEX V2 trading platform
/// </summary>
public class ApexDbContext : DbContext
{
    public ApexDbContext(DbContextOptions<ApexDbContext> options) : base(options) { }

    // Market Data Tables
    public DbSet<StockQuoteEntity> StockQuotes { get; set; }
    public DbSet<HistoricalPriceEntity> HistoricalPrices { get; set; }
    public DbSet<CompanyFundamentalsEntity> CompanyFundamentals { get; set; }
    public DbSet<NewsItemEntity> NewsItems { get; set; }

    // Trading Tables
    public DbSet<AccountEntity> Accounts { get; set; }
    public DbSet<PositionEntity> Positions { get; set; }
    public DbSet<OrderEntity> Orders { get; set; }
    public DbSet<TransactionEntity> Transactions { get; set; }

    // User Configuration Tables
    public DbSet<UserSettingsEntity> UserSettings { get; set; }
    public DbSet<WatchlistEntity> Watchlists { get; set; }
    public DbSet<WatchlistItemEntity> WatchlistItems { get; set; }
    public DbSet<CustomIndicatorEntity> CustomIndicators { get; set; }
    public DbSet<WorkspaceLayoutEntity> WorkspaceLayouts { get; set; }

    // Technical Indicators Cache
    public DbSet<TechnicalIndicatorEntity> TechnicalIndicators { get; set; }

    // User Authentication Tables
    public DbSet<UserEntity> Users { get; set; }
    public DbSet<UserProfileEntity> UserProfiles { get; set; } // added
    public DbSet<IntradayPriceEntity> IntradayPrices { get; set; } // new
    public DbSet<NavigationPageEntity> NavigationPages { get; set; } // navigation pages

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure StockQuotes
        modelBuilder.Entity<StockQuoteEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Symbol);
            entity.HasIndex(e => e.Timestamp);
            entity.Property(e => e.Price).HasPrecision(18, 4);
            entity.Property(e => e.Change).HasPrecision(18, 4);
            entity.Property(e => e.Open).HasPrecision(18, 4);
            entity.Property(e => e.High).HasPrecision(18, 4);
            entity.Property(e => e.Low).HasPrecision(18, 4);
            entity.Property(e => e.Close).HasPrecision(18, 4);
        });

        // Configure HistoricalPrices
        modelBuilder.Entity<HistoricalPriceEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Symbol, e.Date }).IsUnique();
            entity.Property(e => e.Open).HasPrecision(18, 4);
            entity.Property(e => e.High).HasPrecision(18, 4);
            entity.Property(e => e.Low).HasPrecision(18, 4);
            entity.Property(e => e.Close).HasPrecision(18, 4);
        });

        // Configure Orders
        modelBuilder.Entity<OrderEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.OrderId).IsUnique();
            entity.HasIndex(e => e.Symbol);
            entity.Property(e => e.LimitPrice).HasPrecision(18, 4);
            entity.Property(e => e.StopPrice).HasPrecision(18, 4);
            entity.Property(e => e.FilledPrice).HasPrecision(18, 4);
        });

        // Configure Positions
        modelBuilder.Entity<PositionEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.AccountId, e.Symbol }).IsUnique();
            entity.Property(e => e.AveragePrice).HasPrecision(18, 4);
            entity.Property(e => e.CurrentPrice).HasPrecision(18, 4);
        });

        // Configure Transactions
        modelBuilder.Entity<TransactionEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TransactionId).IsUnique();
            entity.HasIndex(e => e.Symbol);
            entity.HasIndex(e => e.Date);
            entity.Property(e => e.Amount).HasPrecision(18, 4);
            entity.Property(e => e.Price).HasPrecision(18, 4);
        });

        // Configure Watchlists
        modelBuilder.Entity<WatchlistEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasMany(e => e.Items)
                  .WithOne(e => e.Watchlist)
                  .HasForeignKey(e => e.WatchlistId);
        });

        // UserEntity configuration
        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UsernameNormalized).IsUnique();
            entity.Property(e => e.Username).IsRequired();
            entity.Property(e => e.UsernameNormalized).IsRequired();
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.PasswordSalt).IsRequired();
            entity.Property(e => e.HashAlgorithm).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Roles).HasMaxLength(200);
            entity.Property(e => e.CreatedUtc).IsRequired();
            entity.Property(e => e.SecurityStamp).IsRequired();
            entity.Property(e => e.MustChangePassword).HasDefaultValue(false); // added
        });

        // UserProfile configuration
        modelBuilder.Entity<UserProfileEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.LastName).HasMaxLength(100);
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.HasIndex(e => e.Email); // added for potential lookups
            entity.Property(e => e.AvatarPath).HasMaxLength(500);
            entity.Property(e => e.CreatedUtc).IsRequired();
            entity.Property(e => e.UpdatedUtc).IsRequired();
            entity.Property(e => e.RowVersion).IsRowVersion(); // configure concurrency token
        });

        // Configure IntradayPrices
        modelBuilder.Entity<IntradayPriceEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Symbol, e.Timestamp, e.IntervalSec }).IsUnique();
            entity.Property(e => e.Open).HasPrecision(18, 4);
            entity.Property(e => e.High).HasPrecision(18, 4);
            entity.Property(e => e.Low).HasPrecision(18, 4);
            entity.Property(e => e.Close).HasPrecision(18, 4);
        });

        modelBuilder.Entity<NavigationPageEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.LayoutName).IsRequired().HasMaxLength(150);
            entity.Property(e => e.SortOrder).HasDefaultValue(0);
            entity.Property(e => e.CreatedUtc).IsRequired();
            entity.Property(e => e.UpdatedUtc).IsRequired();
        });
    }
}

// Entity Models (Database representations)
public class StockQuoteEntity
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal Change { get; set; }
    public decimal ChangePercent { get; set; }
    public long Volume { get; set; }
    public DateTime Timestamp { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public string DataProvider { get; set; } = string.Empty;
}

public class HistoricalPriceEntity
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
    public string DataProvider { get; set; } = string.Empty;
}

public class CompanyFundamentalsEntity
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public decimal MarketCap { get; set; }
    public decimal PE_Ratio { get; set; }
    public decimal EPS { get; set; }
    public decimal DebtToEquity { get; set; }
    public decimal PriceToBook { get; set; }
    public decimal ROE { get; set; }
    public decimal DividendYield { get; set; }
    public string Sector { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
}

public class NewsItemEntity
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public DateTime PublishedDate { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty; // Optional - for stock-specific news
}

public class AccountEntity
{
    public int Id { get; set; }
    public string AccountId { get; set; } = string.Empty;
    public decimal Cash { get; set; }
    public decimal MarketValue { get; set; }
    public decimal BuyingPower { get; set; }
    public bool IsPaperAccount { get; set; }
    public string TradingProvider { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
}

public class PositionEntity
{
    public int Id { get; set; }
    public string AccountId { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal CurrentPrice { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class OrderEntity
{
    public int Id { get; set; }
    public string OrderId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Side { get; set; } = string.Empty; // Buy/Sell
    public string Type { get; set; } = string.Empty; // Market/Limit/Stop
    public decimal? LimitPrice { get; set; }
    public decimal? StopPrice { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? FilledAt { get; set; }
    public int FilledQuantity { get; set; }
    public decimal? FilledPrice { get; set; }
    public bool IsPaperTrade { get; set; }
}

public class TransactionEntity
{
    public int Id { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // Buy/Sell/Dividend/etc
    public decimal Amount { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public DateTime Date { get; set; }
    public bool IsPaperTrade { get; set; }
}

public class UserSettingsEntity
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
}

public class WatchlistEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime Created { get; set; }
    public DateTime LastModified { get; set; }
    public List<WatchlistItemEntity> Items { get; set; } = new();
}

public class WatchlistItemEntity
{
    public int Id { get; set; }
    public int WatchlistId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public DateTime Added { get; set; }
    public WatchlistEntity Watchlist { get; set; } = null!;
}

public class CustomIndicatorEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Formula { get; set; } = string.Empty; // C# code or formula
    public string Parameters { get; set; } = string.Empty; // JSON parameters
    public DateTime Created { get; set; }
    public DateTime LastModified { get; set; }
}

public class WorkspaceLayoutEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string LayoutData { get; set; } = string.Empty; // JSON layout configuration
    public bool IsDefault { get; set; }
    public DateTime Created { get; set; }
    public DateTime LastModified { get; set; }
}

public class TechnicalIndicatorEntity
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string IndicatorName { get; set; } = string.Empty; // SMA, EMA, RSI, etc
    public string Parameters { get; set; } = string.Empty; // JSON parameters like period
    public DateTime Date { get; set; }
    public decimal Value { get; set; }
    public DateTime CalculatedAt { get; set; }
}

public class UserEntity
{
    public int Id { get; set; }
    public Guid PublicId { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public string UsernameNormalized { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty; // Base64
    public string PasswordSalt { get; set; } = string.Empty; // Base64
    public string HashAlgorithm { get; set; } = "PBKDF2-SHA256";
    public int HashIterations { get; set; } = 120_000;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginUtc { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTime? LockoutUntilUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public string Roles { get; set; } = "User"; // comma separated roles
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString();
    public bool MustChangePassword { get; set; } = false; // added
    public DateTime? PasswordLastChangedUtc { get; set; } // added
}

public class UserProfileEntity
{
    public int Id { get; set; }
    public int UserId { get; set; } // FK to UserEntity.Id (simple for now)
    public string DisplayName { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? AvatarPath { get; set; }
    public string? Bio { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>(); // added
}

public class IntradayPriceEntity
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } // bucket start (UTC minute)
    public int IntervalSec { get; set; } = 60; // aggregation interval seconds
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
    public string DataProvider { get; set; } = string.Empty;
}

public class NavigationPageEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string LayoutName { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}