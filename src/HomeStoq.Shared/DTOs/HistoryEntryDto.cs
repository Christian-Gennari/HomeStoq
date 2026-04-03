using System;

namespace HomeStoq.Shared.DTOs;

public record HistoryEntryDto
{
    public long Id { get; init; }
    public DateTime Timestamp { get; init; }
    public string ItemName { get; init; } = string.Empty;
    public string? ExpandedName { get; init; }
    public string Action { get; init; } = string.Empty;
    public double Quantity { get; init; }
    public double? Price { get; init; }
    public double? TotalPrice { get; init; }
    public string? Currency { get; init; }
    public string Source { get; init; } = string.Empty;
    public long? ReceiptId { get; init; }
}
