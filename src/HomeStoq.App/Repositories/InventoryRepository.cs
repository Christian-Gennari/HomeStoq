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

    // BuyList Methods
    public async Task<BuyList?> GetDraftOrActiveBuyListAsync()
    {
        return await _context.BuyLists
            .Include(l => l.Items)
            .Where(l => l.Status == BuyListStatus.Draft || l.Status == BuyListStatus.Active)
            .OrderByDescending(l => l.UpdatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<BuyList> CreateBuyListAsync(string greeting, string? generatedContext = null)
    {
        var now = DateTime.UtcNow;
        var buyList = new BuyList
        {
            CreatedAt = now,
            UpdatedAt = now,
            Status = BuyListStatus.Draft,
            GeneratedContext = generatedContext,
            TotalItems = 0,
            CheckedItems = 0
        };
        _context.BuyLists.Add(buyList);
        await _context.SaveChangesAsync();
        return buyList;
    }

    public async Task<BuyListItem> AddItemToBuyListAsync(long buyListId, string itemName, double quantity, string source, string? aiReasoning = null, bool isChecked = true)
    {
        var now = DateTime.UtcNow;
        var item = new BuyListItem
        {
            BuyListId = buyListId,
            ItemName = itemName,
            Quantity = quantity,
            Source = source,
            AIOriginalReasoning = aiReasoning,
            IsChecked = isChecked,
            IsDismissed = false,
            CreatedAt = now
        };
        _context.BuyListItems.Add(item);
        
        // Update totals
        var buyList = await _context.BuyLists.FindAsync(buyListId);
        if (buyList != null)
        {
            buyList.TotalItems++;
            if (isChecked) buyList.CheckedItems++;
            buyList.UpdatedAt = now;
        }
        
        await _context.SaveChangesAsync();
        return item;
    }

    public async Task UpdateBuyListItemAsync(long itemId, bool? isChecked = null, double? quantity = null, bool? isDismissed = null, string? note = null)
    {
        var item = await _context.BuyListItems.FindAsync(itemId);
        if (item == null) return;

        var buyList = await _context.BuyLists.FindAsync(item.BuyListId);
        if (buyList == null) return;

        var now = DateTime.UtcNow;

        if (isChecked.HasValue && item.IsChecked != isChecked.Value)
        {
            item.IsChecked = isChecked.Value;
            buyList.CheckedItems += isChecked.Value ? 1 : -1;
        }

        if (quantity.HasValue)
        {
            item.Quantity = quantity.Value;
        }

        if (isDismissed.HasValue && item.IsDismissed != isDismissed.Value)
        {
            item.IsDismissed = isDismissed.Value;
            item.DismissedAt = isDismissed.Value ? now : null;
        }

        if (note != null)
        {
            item.Note = note;
        }

        buyList.UpdatedAt = now;
        await _context.SaveChangesAsync();
    }

    public async Task<BuyList?> GetBuyListByIdAsync(long id)
    {
        return await _context.BuyLists
            .Include(l => l.Items)
            .FirstOrDefaultAsync(l => l.Id == id);
    }

    public async Task<IEnumerable<BuyList>> GetBuyListHistoryAsync(int limit = 20)
    {
        return await _context.BuyLists
            .Where(l => l.Status == BuyListStatus.Completed || l.Status == BuyListStatus.Cancelled)
            .OrderByDescending(l => l.CreatedAt)
            .Take(limit)
            .Select(l => new BuyList
            {
                Id = l.Id,
                CreatedAt = l.CreatedAt,
                Status = l.Status,
                SavedName = l.SavedName,
                TotalItems = l.TotalItems,
                CheckedItems = l.CheckedItems,
                GeneratedContext = l.GeneratedContext,
                UserContext = l.UserContext,
                Items = l.Items.Where(i => !i.IsDismissed).ToList()
            })
            .ToListAsync();
    }

    public async Task CommitBuyListAsync(long buyListId)
    {
        var buyList = await _context.BuyLists.FindAsync(buyListId);
        if (buyList == null) return;

        buyList.Status = BuyListStatus.Active;
        buyList.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task CompleteBuyListAsync(long buyListId)
    {
        var buyList = await _context.BuyLists.FindAsync(buyListId);
        if (buyList == null) return;

        buyList.Status = BuyListStatus.Completed;
        buyList.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task UpdateBuyListUserContextAsync(long buyListId, string userContext)
    {
        var buyList = await _context.BuyLists.FindAsync(buyListId);
        if (buyList == null) return;

        buyList.UserContext = userContext;
        buyList.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task ClearBuyListItemsAsync(long buyListId)
    {
        var items = await _context.BuyListItems
            .Where(i => i.BuyListId == buyListId)
            .ToListAsync();
        
        _context.BuyListItems.RemoveRange(items);
        
        var buyList = await _context.BuyLists.FindAsync(buyListId);
        if (buyList != null)
        {
            buyList.TotalItems = 0;
            buyList.CheckedItems = 0;
            buyList.UpdatedAt = DateTime.UtcNow;
        }
        
        await _context.SaveChangesAsync();
    }

    // NEW: Conversation message methods
    public async Task AddBuyListMessageAsync(BuyListMessage message)
    {
        _context.BuyListMessages.Add(message);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateBuyListAsync(BuyList buyList)
    {
        _context.BuyLists.Update(buyList);
        await _context.SaveChangesAsync();
    }

    public async Task<BuyList?> GetSavedBuyListAsync()
    {
        return await _context.BuyLists
            .Include(l => l.Items)
            .Where(l => l.IsSaved && l.IsActiveSession)
            .OrderByDescending(l => l.UpdatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<List<BuyList>> GetAllSavedListsAsync()
    {
        return await _context.BuyLists
            .Include(l => l.Items)
            .Where(l => l.IsSaved && l.Status != BuyListStatus.Completed && l.Status != BuyListStatus.Cancelled)
            .OrderByDescending(l => l.UpdatedAt)
            .ToListAsync();
    }

    public async Task DeleteBuyListAsync(long buyListId)
    {
        var buyList = await _context.BuyLists.FindAsync(buyListId);
        if (buyList != null)
        {
            _context.BuyLists.Remove(buyList);
            await _context.SaveChangesAsync();
        }
    }
}
