using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HomeStoq.App.Models;

public class InventoryItem
{
    [Key]
    public long Id { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public string? Category { get; set; }
    public double? LastPrice { get; set; }
    public string? Currency { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class Receipt
{
    [Key]
    public long Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public double TotalAmountPaid { get; set; }
    
    public virtual ICollection<HistoryEntry> HistoryEntries { get; set; } = new List<HistoryEntry>();
}

public class HistoryEntry
{
    [Key]
    public long Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? ExpandedName { get; set; }
    public string Action { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public double? Price { get; set; }
    public double? TotalPrice { get; set; }
    public string? Currency { get; set; }
    public string Source { get; set; } = string.Empty;
    
    public long? ReceiptId { get; set; }
    [ForeignKey("ReceiptId")]
    public virtual Receipt? Receipt { get; set; }
}

public class AiCacheEntry
{
    [Key]
    public long Id { get; set; }
    public string CacheKey { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
