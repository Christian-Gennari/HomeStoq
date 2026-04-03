using System;

namespace HomeStoq.Contracts;

public record InventoryItemDto(
    int Id = 0,
    string ItemName = "",
    double Quantity = 0,
    string? Category = null,
    double? LastPrice = null,
    string? Currency = null,
    DateTime UpdatedAt = default
);