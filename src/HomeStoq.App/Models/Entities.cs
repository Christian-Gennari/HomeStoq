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

public class BuyList
{
    [Key]
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public BuyListStatus Status { get; set; }
    public string? GeneratedContext { get; set; }
    public string? UserContext { get; set; }
    public int TotalItems { get; set; }
    public int CheckedItems { get; set; }
    
    // NEW: For conversational shopping list factory
    public string? ConversationJson { get; set; }
    public bool IsSaved { get; set; }
    public string? SavedName { get; set; }
    public bool IsActiveSession { get; set; }
    
    public virtual ICollection<BuyListItem> Items { get; set; } = new List<BuyListItem>();
    public virtual ICollection<BuyListMessage> Messages { get; set; } = new List<BuyListMessage>();
}

public enum BuyListStatus
{
    Draft,
    Active,
    Completed,
    Cancelled,
    Saved
}

public class BuyListItem
{
    [Key]
    public long Id { get; set; }
    public long BuyListId { get; set; }
    public virtual BuyList BuyList { get; set; } = null!;
    public string ItemName { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public string? Note { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? AIOriginalReasoning { get; set; }
    public bool IsChecked { get; set; }
    public bool IsDismissed { get; set; }
    public DateTime? DismissedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

// NEW: Store individual messages for conversation history
public class BuyListMessage
{
    [Key]
    public long Id { get; set; }
    public long BuyListId { get; set; }
    public virtual BuyList BuyList { get; set; } = null!;
    public string Role { get; set; } = string.Empty; // "system", "user", "assistant"
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? ActionsJson { get; set; } // JSON array of actions performed ["added:Milk:2", "removed:Salsa"]
}
