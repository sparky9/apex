using Microsoft.EntityFrameworkCore;
using System;
using System.IO;

namespace ApexV2.Backtesting.Database
{
    /// <summary>
    /// Database context for backtesting data
    /// </summary>
    public class BacktestingDbContext : DbContext
    {
        public DbSet<SavedStrategyEntity> SavedStrategies { get; set; }
        public DbSet<BacktestResultEntity> BacktestResults { get; set; }
        public DbSet<StrategyTemplateEntity> StrategyTemplates { get; set; }
        public DbSet<BatchBacktestSessionEntity> BatchBacktestSessions { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // Use the same database path as the main Apex database
                var dbPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Apex",
                    "backtesting.db");

                // Ensure directory exists
                var directory = Path.GetDirectoryName(dbPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                optionsBuilder.UseSqlite($"Data Source={dbPath}");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure relationships
            modelBuilder.Entity<SavedStrategyEntity>()
                .HasMany(s => s.BacktestResults)
                .WithOne(r => r.Strategy)
                .HasForeignKey(r => r.StrategyId)
                .OnDelete(DeleteBehavior.Cascade);

            // Add indexes for performance
            modelBuilder.Entity<SavedStrategyEntity>()
                .HasIndex(s => s.Name);

            modelBuilder.Entity<SavedStrategyEntity>()
                .HasIndex(s => s.StrategyType);

            modelBuilder.Entity<BacktestResultEntity>()
                .HasIndex(r => r.StrategyId);

            modelBuilder.Entity<BacktestResultEntity>()
                .HasIndex(r => r.Symbol);

            modelBuilder.Entity<BacktestResultEntity>()
                .HasIndex(r => r.RunDate);

            modelBuilder.Entity<StrategyTemplateEntity>()
                .HasIndex(t => t.StrategyType);

            modelBuilder.Entity<BatchBacktestSessionEntity>()
                .HasIndex(b => b.StartTime);
        }
    }
}
