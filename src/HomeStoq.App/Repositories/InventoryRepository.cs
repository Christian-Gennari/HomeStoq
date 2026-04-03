using System.ComponentModel;
using HomeStoq.App.Data;
using HomeStoq.App.Models;
using HomeStoq.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace HomeStoq.App.Repositories;

public class InventoryRepository
{
    private readonly PantryDbContext _context;
    private readonly ILogger<InventoryRepository> _logger;

    public InventoryRepository(PantryDbContext context, ILogger<InventoryRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<InventoryItemDto>> GetInventoryAsync()
    {
        try
        {
            var items = await _context.Inventory
                .OrderBy(i => i.ItemName)
                .Select(i => new InventoryItemDto
                {
                    Id = i.Id,
                    ItemName = i.ItemName,
                    Quantity = i.Quantity,
                    Category = i.Category,
                    LastPrice = i.LastPrice,
                    Currency = i.Currency,
                    UpdatedAt = i.UpdatedAt
                })
                .ToListAsync();
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
        var receipt = new Receipt
        {
            Timestamp = DateTime.UtcNow,
            StoreName = storeName,
            TotalAmountPaid = totalAmount
        };
        _context.Receipts.Add(receipt);
        await _context.SaveChangesAsync();
        return receipt.Id;
    }

    public async Task<IEnumerable<ReceiptDto>> GetReceiptsAsync()
    {
        return await _context.Receipts
            .OrderByDescending(r => r.Timestamp)
            .Select(r => new ReceiptDto
            {
                Id = r.Id,
                Timestamp = r.Timestamp,
                StoreName = r.StoreName,
                TotalAmountPaid = r.TotalAmountPaid
            })
            .ToListAsync();
    }

    public async Task<IEnumerable<HistoryEntryDto>> GetReceiptItemsAsync(long receiptId)
    {
        return await _context.History
            .Where(h => h.ReceiptId == receiptId)
            .Select(h => new HistoryEntryDto
            {
                Id = h.Id,
                Timestamp = h.Timestamp,
                ItemName = h.ItemName,
                ExpandedName = h.ExpandedName,
                Action = h.Action,
                Quantity = h.Quantity,
                Price = h.Price,
                TotalPrice = h.TotalPrice,
                Currency = h.Currency,
                Source = h.Source,
                ReceiptId = h.ReceiptId
            })
            .ToListAsync();
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

        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var existingItem = await _context.Inventory
                .FirstOrDefaultAsync(i => EF.Functions.Like(i.ItemName, itemName));

            var now = DateTime.UtcNow;
            var action = quantityChange >= 0 ? "Add" : "Remove";
            var absQuantity = Math.Abs(quantityChange);

            if (existingItem == null)
            {
                if (quantityChange < 0)
                    return;

                _context.Inventory.Add(new InventoryItem
                {
                    ItemName = itemName,
                    Quantity = quantityChange,
                    Category = category,
                    LastPrice = price,
                    Currency = currency,
                    UpdatedAt = now
                });
            }
            else
            {
                existingItem.Quantity = Math.Max(0, existingItem.Quantity + quantityChange);
                if (category != null) existingItem.Category = category;
                if (price != null) existingItem.LastPrice = price;
                if (currency != null) existingItem.Currency = currency;
                existingItem.UpdatedAt = now;
            }

            _context.History.Add(new HistoryEntry
            {
                Timestamp = now,
                ItemName = itemName,
                ExpandedName = expandedName ?? itemName,
                Action = action,
                Quantity = absQuantity,
                Price = price,
                TotalPrice = price.HasValue ? price.Value * absQuantity : null,
                Currency = currency,
                Source = source,
                ReceiptId = receiptId
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update inventory item: {ItemName}", itemName);
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<IEnumerable<HistoryEntryDto>> GetHistoryAsync(int days = 30)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-days);
        return await _context.History
            .Where(h => h.Timestamp >= cutoffDate)
            .OrderByDescending(h => h.Timestamp)
            .Select(h => new HistoryEntryDto
            {
                Id = h.Id,
                Timestamp = h.Timestamp,
                ItemName = h.ItemName,
                ExpandedName = h.ExpandedName,
                Action = h.Action,
                Quantity = h.Quantity,
                Price = h.Price,
                TotalPrice = h.TotalPrice,
                Currency = h.Currency,
                Source = h.Source,
                ReceiptId = h.ReceiptId
            })
            .ToListAsync();
    }

    // AI Tool Methods
    [Description("Gets the current stock level for a specific item.")]
    public async Task<double> GetStockLevel(string itemName)
    {
        return await _context.Inventory
            .Where(i => EF.Functions.Like(i.ItemName, itemName))
            .Select(i => i.Quantity)
            .FirstOrDefaultAsync();
    }

    [Description("Gets the full list of items currently in the inventory and their quantities.")]
    public async Task<IEnumerable<InventoryItemDto>> GetFullInventory()
    {
        return await GetInventoryAsync();
    }

    [Description(
        "Gets the consumption/purchase history for the last X days, optionally filtered by category."
    )]
    public async Task<IEnumerable<HistoryEntryDto>> GetConsumptionHistory(
        int days,
        string? category = null
    )
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-days);
        var query = _context.History.AsQueryable();

        if (!string.IsNullOrEmpty(category))
        {
            query = query.Where(h => _context.Inventory.Any(i => i.ItemName == h.ItemName && i.Category == category));
        }

        return await query
            .Where(h => h.Timestamp >= cutoffDate)
            .OrderByDescending(h => h.Timestamp)
            .Select(h => new HistoryEntryDto
            {
                Id = h.Id,
                Timestamp = h.Timestamp,
                ItemName = h.ItemName,
                ExpandedName = h.ExpandedName,
                Action = h.Action,
                Quantity = h.Quantity,
                Price = h.Price,
                TotalPrice = h.TotalPrice,
                Currency = h.Currency,
                Source = h.Source,
                ReceiptId = h.ReceiptId
            })
            .ToListAsync();
    }

    public async Task<string?> GetAiCacheAsync(string cacheKey)
    {
        var now = DateTime.UtcNow;
        return await _context.AiCache
            .Where(c => c.CacheKey == cacheKey && c.ExpiresAt > now)
            .Select(c => c.Response)
            .FirstOrDefaultAsync();
    }

    public async Task SetAiCacheAsync(string cacheKey, string response, TimeSpan ttl)
    {
        var now = DateTime.UtcNow;
        var entry = await _context.AiCache.FirstOrDefaultAsync(c => c.CacheKey == cacheKey);
        
        if (entry == null)
        {
            _context.AiCache.Add(new AiCacheEntry
            {
                CacheKey = cacheKey,
                Response = response,
                CreatedAt = now,
                ExpiresAt = now.Add(ttl)
            });
        }
        else
        {
            entry.Response = response;
            entry.CreatedAt = now;
            entry.ExpiresAt = now.Add(ttl);
        }
        
        await _context.SaveChangesAsync();
    }
}
