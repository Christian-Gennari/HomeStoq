namespace HomeStoq.Contracts;

public record HistoryEntryDto
{
    public int Id { get; init; }
    public DateTime Timestamp { get; init; }
    public string ItemName { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty; // "Add" or "Remove"
    public double Quantity { get; init; }
    public double? Price { get; init; }
    public double? TotalPrice { get; init; }
    public string? Currency { get; init; }
    public string Source { get; init; } = string.Empty; // "ReceiptDto", "Voice", "Manual"
}
