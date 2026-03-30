using Dapper;
using HomeStoq.Server.Models;
using Microsoft.Data.Sqlite;

namespace HomeStoq.Server.Repositories;

public class InventoryRepository
{
    private readonly string _connectionString;

    public InventoryRepository(IConfiguration configuration)
    {
        var dbPath = configuration["DATABASE_PATH"] ?? "homestoq.db";
        _connectionString = $"Data Source={dbPath}";
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        connection.Execute(@"
            CREATE TABLE IF NOT EXISTS Inventory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ItemName TEXT UNIQUE NOT NULL,
                Quantity REAL NOT NULL DEFAULT 0,
                LastPrice REAL,
                Currency TEXT,
                UpdatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS History (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Timestamp TEXT NOT NULL,
                ItemName TEXT NOT NULL,
                Action TEXT NOT NULL,
                Quantity REAL NOT NULL,
                Price REAL,
                TotalPrice REAL,
                Currency TEXT,
                Source TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS AiCache (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CacheKey TEXT UNIQUE NOT NULL,
                Response TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                ExpiresAt TEXT NOT NULL
            );
        ");
    }

    public async Task<IEnumerable<InventoryItem>> GetInventoryAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        return await connection.QueryAsync<InventoryItem>("SELECT * FROM Inventory ORDER BY ItemName");
    }

    public async Task UpdateInventoryItemAsync(string itemName, double quantityChange, double? price = null, string? currency = null, string source = "Manual")
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = await connection.BeginTransactionAsync();

        try
        {
            var existingItem = await connection.QueryFirstOrDefaultAsync<InventoryItem>(
                "SELECT * FROM Inventory WHERE ItemName = @ItemName", new { ItemName = itemName }, transaction);

            var now = DateTime.UtcNow.ToString("O");
            var action = quantityChange >= 0 ? "Add" : "Remove";
            var absQuantity = Math.Abs(quantityChange);

            if (existingItem == null)
            {
                if (quantityChange < 0) return; // Can't remove what doesn't exist

                await connection.ExecuteAsync(@"
                    INSERT INTO Inventory (ItemName, Quantity, LastPrice, Currency, UpdatedAt)
                    VALUES (@ItemName, @Quantity, @LastPrice, @Currency, @UpdatedAt)",
                    new { ItemName = itemName, Quantity = quantityChange, LastPrice = price, Currency = currency, UpdatedAt = now },
                    transaction);
            }
            else
            {
                var newQuantity = Math.Max(0, existingItem.Quantity + quantityChange);
                await connection.ExecuteAsync(@"
                    UPDATE Inventory 
                    SET Quantity = @Quantity, LastPrice = COALESCE(@LastPrice, LastPrice), Currency = COALESCE(@Currency, Currency), UpdatedAt = @UpdatedAt
                    WHERE ItemName = @ItemName",
                    new { ItemName = itemName, Quantity = newQuantity, LastPrice = price, Currency = currency, UpdatedAt = now },
                    transaction);
            }

            // Log to History
            await connection.ExecuteAsync(@"
                INSERT INTO History (Timestamp, ItemName, Action, Quantity, Price, TotalPrice, Currency, Source)
                VALUES (@Timestamp, @ItemName, @Action, @Quantity, @Price, @TotalPrice, @Currency, @Source)",
                new 
                { 
                    Timestamp = now, 
                    ItemName = itemName, 
                    Action = action, 
                    Quantity = absQuantity, 
                    Price = price, 
                    TotalPrice = price.HasValue ? price.Value * absQuantity : (double?)null,
                    Currency = currency,
                    Source = source
                },
                transaction);

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<IEnumerable<HistoryEntry>> GetHistoryAsync(int days = 30)
    {
        using var connection = new SqliteConnection(_connectionString);
        var cutoffDate = DateTime.UtcNow.AddDays(-days).ToString("O");
        return await connection.QueryAsync<HistoryEntry>(
            "SELECT * FROM History WHERE Timestamp >= @CutoffDate ORDER BY Timestamp DESC",
            new { CutoffDate = cutoffDate });
    }

    public async Task<string?> GetAiCacheAsync(string cacheKey)
    {
        using var connection = new SqliteConnection(_connectionString);
        var now = DateTime.UtcNow.ToString("O");
        return await connection.QueryFirstOrDefaultAsync<string>(
            "SELECT Response FROM AiCache WHERE CacheKey = @CacheKey AND ExpiresAt > @Now",
            new { CacheKey = cacheKey, Now = now });
    }

    public async Task SetAiCacheAsync(string cacheKey, string response, TimeSpan ttl)
    {
        using var connection = new SqliteConnection(_connectionString);
        var now = DateTime.UtcNow;
        await connection.ExecuteAsync(@"
            INSERT OR REPLACE INTO AiCache (CacheKey, Response, CreatedAt, ExpiresAt)
            VALUES (@CacheKey, @Response, @CreatedAt, @ExpiresAt)",
            new 
            { 
                CacheKey = cacheKey, 
                Response = response, 
                CreatedAt = now.ToString("O"), 
                ExpiresAt = now.Add(ttl).ToString("O") 
            });
    }
}
