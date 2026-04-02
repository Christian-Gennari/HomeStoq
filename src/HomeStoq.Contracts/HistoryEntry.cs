namespace HomeStoq.Contracts;

public class HistoryEntry
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty; // "Add" or "Remove"
    public double Quantity { get; set; }
    public double? Price { get; set; }
    public double? TotalPrice { get; set; }
    public string? Currency { get; set; }
    public string Source { get; set; } = string.Empty; // "Receipt", "Voice", "Manual"
}
