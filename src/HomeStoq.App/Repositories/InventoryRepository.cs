using System.ComponentModel;
using Dapper;
using HomeStoq.Contracts;
using HomeStoq.Contracts.SharedUtils;
using Microsoft.Data.Sqlite;

namespace HomeStoq.App.Repositories;

public class InventoryRepository
{
    private readonly string _connectionString;
    private readonly ILogger<InventoryRepository> _logger;

    public InventoryRepository(ILogger<InventoryRepository> logger)
    {
        _logger = logger;

        var dbPath = PathHelper.ResolveDatabasePath();

        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = $"Data Source={dbPath}";
    }

    public async Task<IEnumerable<InventoryItem>> GetInventoryAsync()
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            var items = await connection.QueryAsync<InventoryItem>(
                "SELECT * FROM Inventory ORDER BY ItemName"
            );
            return items;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching inventory.");
            throw;
        }
    }

    public async Task<long> CreateReceiptAsync(string storeName, double totalAmount)
    {
        using var connection = new SqliteConnection(_connectionString);
        var now = DateTime.UtcNow.ToString("O");
        return await connection.QuerySingleAsync<long>(
            @"
            INSERT INTO Receipts (Timestamp, StoreName, TotalAmountPaid)
            VALUES (@Timestamp, @StoreName, @TotalAmountPaid);
            SELECT last_insert_rowid();",
            new
            {
                Timestamp = now,
                StoreName = storeName,
                TotalAmountPaid = totalAmount,
            }
        );
    }

    public async Task<IEnumerable<Receipt>> GetReceiptsAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        return await connection.QueryAsync<Receipt>(
            "SELECT * FROM Receipts ORDER BY Timestamp DESC"
        );
    }

    public async Task<IEnumerable<HistoryEntry>> GetReceiptItemsAsync(long receiptId)
    {
        using var connection = new SqliteConnection(_connectionString);
        return await connection.QueryAsync<HistoryEntry>(
            "SELECT * FROM History WHERE ReceiptId = @ReceiptId",
            new { ReceiptId = receiptId }
        );
    }

    public async Task UpdateInventoryItemAsync(
        string itemName,
        double quantityChange,
        double? price = null,
        string? currency = null,
        string source = "Manual",
        string? category = null,
        long? receiptId = null,
        string? expandedName = null
    )
    {
        _logger.LogInformation(
            "Updating inventory: {ItemName} ({Change}) via {Source}",
            itemName,
            quantityChange,
            source
        );
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = await connection.BeginTransactionAsync();

        try
        {
            var existingItem = await connection.QueryFirstOrDefaultAsync<InventoryItem>(
                "SELECT * FROM Inventory WHERE ItemName = @ItemName COLLATE NOCASE",
                new { ItemName = itemName },
                transaction
            );

            var now = DateTime.UtcNow.ToString("O");
            var action = quantityChange >= 0 ? "Add" : "Remove";
            var absQuantity = Math.Abs(quantityChange);

            if (existingItem == null)
            {
                if (quantityChange < 0)
                    return;

                await connection.ExecuteAsync(
                    @"
                    INSERT INTO Inventory (ItemName, Quantity, Category, LastPrice, Currency, UpdatedAt)
                    VALUES (@ItemName, @Quantity, @Category, @LastPrice, @Currency, @UpdatedAt)",
                    new
                    {
                        ItemName = itemName,
                        Quantity = quantityChange,
                        Category = category,
                        LastPrice = price,
                        Currency = currency,
                        UpdatedAt = now,
                    },
                    transaction
                );
            }
            else
            {
                var newQuantity = Math.Max(0, existingItem.Quantity + quantityChange);
                await connection.ExecuteAsync(
                    @"
                    UPDATE Inventory 
                    SET Quantity = @Quantity, 
                        Category = COALESCE(@Category, Category),
                        LastPrice = COALESCE(@LastPrice, LastPrice), 
                        Currency = COALESCE(@Currency, Currency), 
                        UpdatedAt = @UpdatedAt
                    WHERE ItemName = @ItemName",
                    new
                    {
                        ItemName = itemName,
                        Quantity = newQuantity,
                        Category = category,
                        LastPrice = price,
                        Currency = currency,
                        UpdatedAt = now,
                    },
                    transaction
                );
            }

            await connection.ExecuteAsync(
                @"
                INSERT INTO History (Timestamp, ItemName, ExpandedName, Action, Quantity, Price, TotalPrice, Currency, Source, ReceiptId)
                VALUES (@Timestamp, @ItemName, @ExpandedName, @Action, @Quantity, @Price, @TotalPrice, @Currency, @Source, @ReceiptId)",
                new
                {
                    Timestamp = now,
                    ItemName = itemName,
                    ExpandedName = expandedName ?? itemName,
                    Action = action,
                    Quantity = absQuantity,
                    Price = price,
                    TotalPrice = price.HasValue ? price.Value * absQuantity : (double?)null,
                    Currency = currency,
                    Source = source,
                    ReceiptId = receiptId,
                },
                transaction
            );

            await transaction.CommitAsync();
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
        using var connection = new SqliteConnection(_connectionString);
        var cutoffDate = DateTime.UtcNow.AddDays(-days).ToString("O");
        return await connection.QueryAsync<HistoryEntry>(
            "SELECT * FROM History WHERE Timestamp >= @CutoffDate ORDER BY Timestamp DESC",
            new { CutoffDate = cutoffDate }
        );
    }

    // AI Tool Methods
    [Description("Gets the current stock level for a specific item.")]
    public async Task<double> GetStockLevel(string itemName)
    {
        using var connection = new SqliteConnection(_connectionString);
        return await connection.QueryFirstOrDefaultAsync<double>(
            "SELECT Quantity FROM Inventory WHERE ItemName = @ItemName COLLATE NOCASE",
            new { ItemName = itemName }
        );
    }

    [Description("Gets the full list of items currently in the inventory and their quantities.")]
    public async Task<IEnumerable<InventoryItem>> GetFullInventory()
    {
        return await GetInventoryAsync();
    }

    [Description(
        "Gets the consumption/purchase history for the last X days, optionally filtered by category."
    )]
    public async Task<IEnumerable<HistoryEntry>> GetConsumptionHistory(
        int days,
        string? category = null
    )
    {
        using var connection = new SqliteConnection(_connectionString);
        var cutoffDate = DateTime.UtcNow.AddDays(-days).ToString("O");
        var query = "SELECT h.* FROM History h ";
        if (!string.IsNullOrEmpty(category))
        {
            query +=
                "JOIN Inventory i ON h.ItemName = i.ItemName WHERE i.Category = @Category AND h.Timestamp >= @CutoffDate ";
        }
        else
        {
            query += "WHERE h.Timestamp >= @CutoffDate ";
        }
        query += "ORDER BY h.Timestamp DESC";

        return await connection.QueryAsync<HistoryEntry>(
            query,
            new { CutoffDate = cutoffDate, Category = category }
        );
    }

    public async Task<string?> GetAiCacheAsync(string cacheKey)
    {
        using var connection = new SqliteConnection(_connectionString);
        var now = DateTime.UtcNow.ToString("O");
        return await connection.QueryFirstOrDefaultAsync<string>(
            "SELECT Response FROM AiCache WHERE CacheKey = @CacheKey AND ExpiresAt > @Now",
            new { CacheKey = cacheKey, Now = now }
        );
    }

    public async Task SetAiCacheAsync(string cacheKey, string response, TimeSpan ttl)
    {
        using var connection = new SqliteConnection(_connectionString);
        var now = DateTime.UtcNow;
        await connection.ExecuteAsync(
            @"
            INSERT OR REPLACE INTO AiCache (CacheKey, Response, CreatedAt, ExpiresAt)
            VALUES (@CacheKey, @Response, @CreatedAt, @ExpiresAt)",
            new
            {
                CacheKey = cacheKey,
                Response = response,
                CreatedAt = now.ToString("O"),
                ExpiresAt = now.Add(ttl).ToString("O"),
            }
        );
    }
}
