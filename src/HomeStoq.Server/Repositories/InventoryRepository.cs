using Dapper;
using HomeStoq.Server.Models;
using Microsoft.Data.Sqlite;

namespace HomeStoq.Server.Repositories;

public class InventoryRepository
{
    private readonly string _connectionString;
    private readonly ILogger<InventoryRepository> _logger;

    public InventoryRepository(IConfiguration configuration, ILogger<InventoryRepository> logger)
    {
        _logger = logger;
        var dbPath = configuration["Database:Path"] ?? configuration["DATABASE_PATH"] ?? "homestoq.db";
        _connectionString = $"Data Source={dbPath}";
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            _logger.LogInformation("Initializing database at {ConnectionString}", _connectionString);
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
            _logger.LogInformation("Database tables initialized successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to initialize database.");
            throw;
        }
    }

    public async Task<IEnumerable<InventoryItem>> GetInventoryAsync()
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            var items = await connection.QueryAsync<InventoryItem>("SELECT * FROM Inventory ORDER BY ItemName");
            _logger.LogDebug("Fetched {Count} inventory items.", items.Count());
            return items;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching inventory.");
            throw;
        }
    }

    public async Task UpdateInventoryItemAsync(string itemName, double quantityChange, double? price = null, string? currency = null, string source = "Manual")
    {
        _logger.LogInformation("Updating inventory: {ItemName} ({Change}) via {Source}", itemName, quantityChange, source);
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = await connection.BeginTransactionAsync();

        try
        {
            // Try exact match first, then case-insensitive
            var existingItem = await connection.QueryFirstOrDefaultAsync<InventoryItem>(
                "SELECT * FROM Inventory WHERE ItemName = @ItemName COLLATE NOCASE", new { ItemName = itemName }, transaction);

            var now = DateTime.UtcNow.ToString("O");
            var action = quantityChange >= 0 ? "Add" : "Remove";
            var absQuantity = Math.Abs(quantityChange);

            if (existingItem == null)
            {
                if (quantityChange < 0)
                {
                    _logger.LogWarning("Attempted to remove non-existent item: {ItemName}", itemName);
                    return;
                }

                await connection.ExecuteAsync(@"
                    INSERT INTO Inventory (ItemName, Quantity, LastPrice, Currency, UpdatedAt)
                    VALUES (@ItemName, @Quantity, @LastPrice, @Currency, @UpdatedAt)",
                    new { ItemName = itemName, Quantity = quantityChange, LastPrice = price, Currency = currency, UpdatedAt = now },
                    transaction);
                _logger.LogInformation("Added new inventory item: {ItemName} with qty {Quantity}", itemName, quantityChange);
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
                _logger.LogInformation("Updated {ItemName}: {OldQty} -> {NewQty}", itemName, existingItem.Quantity, newQuantity);
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
            _logger.LogDebug("Inventory update transaction committed for {ItemName}", itemName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update inventory item: {ItemName}", itemName);
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<IEnumerable<HistoryEntry>> GetHistoryAsync(int days = 30)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            var cutoffDate = DateTime.UtcNow.AddDays(-days).ToString("O");
            var history = await connection.QueryAsync<HistoryEntry>(
                "SELECT * FROM History WHERE Timestamp >= @CutoffDate ORDER BY Timestamp DESC",
                new { CutoffDate = cutoffDate });
            _logger.LogDebug("Fetched {Count} history entries for last {Days} days.", history.Count(), days);
            return history;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching history for last {Days} days.", days);
            throw;
        }
    }

    public async Task<string?> GetAiCacheAsync(string cacheKey)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            var now = DateTime.UtcNow.ToString("O");
            var response = await connection.QueryFirstOrDefaultAsync<string>(
                "SELECT Response FROM AiCache WHERE CacheKey = @CacheKey AND ExpiresAt > @Now",
                new { CacheKey = cacheKey, Now = now });
            
            if (response != null) _logger.LogDebug("AI Cache HIT for key {CacheKey}", cacheKey);
            else _logger.LogDebug("AI Cache MISS for key {CacheKey}", cacheKey);
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error reading from AI Cache.");
            return null;
        }
    }

    public async Task SetAiCacheAsync(string cacheKey, string response, TimeSpan ttl)
    {
        try
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
            _logger.LogDebug("Saved AI response to cache with key {CacheKey} (TTL: {TTL})", cacheKey, ttl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error writing to AI Cache.");
        }
    }
}
