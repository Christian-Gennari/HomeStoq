using System;

namespace HomeStoq.Contracts;

public record HistoryEntryDto(
    int Id = 0,
    DateTime Timestamp = default,
    string ItemName = "",
    string Action = "",
    double Quantity = 0,
    double? Price = null,
    double? TotalPrice = null,
    string? Currency = null,
    string Source = ""
);