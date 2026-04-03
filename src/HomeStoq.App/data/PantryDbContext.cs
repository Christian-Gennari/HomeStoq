using HomeStoq.App.Models;
using Microsoft.EntityFrameworkCore;

namespace HomeStoq.App.Data;

public class PantryDbContext : DbContext
{
    public PantryDbContext(DbContextOptions<PantryDbContext> options)
        : base(options)
    {
    }

    public DbSet<InventoryItem> Inventory { get; set; } = null!;
    public DbSet<Receipt> Receipts { get; set; } = null!;
    public DbSet<HistoryEntry> History { get; set; } = null!;
    public DbSet<AiCacheEntry> AiCache { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.ToTable("Inventory");
            entity.HasIndex(e => e.ItemName).IsUnique();
        });

        modelBuilder.Entity<Receipt>(entity =>
        {
            entity.ToTable("Receipts");
        });

        modelBuilder.Entity<HistoryEntry>(entity =>
        {
            entity.ToTable("History");
        });

        modelBuilder.Entity<AiCacheEntry>(entity =>
        {
            entity.ToTable("AiCache");
            entity.HasIndex(e => e.CacheKey).IsUnique();
        });
    }
}
