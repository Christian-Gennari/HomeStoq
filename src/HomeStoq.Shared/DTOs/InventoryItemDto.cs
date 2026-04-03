using System;

namespace HomeStoq.Shared.DTOs;

public record InventoryItemDto
{
    public long Id { get; init; }
    public string ItemName { get; init; } = string.Empty;
    public double Quantity { get; init; }
    public string? Category { get; init; }
    public double? LastPrice { get; init; }
    public string? Currency { get; init; }
    public DateTime UpdatedAt { get; init; }
}
