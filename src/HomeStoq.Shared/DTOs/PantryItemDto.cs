namespace HomeStoq.Shared.DTOs;

public record PantryItemDto(
    string ReceiptText = "",
    string ExpandedName = "",
    string ItemName = "",
    double Quantity = 0,
    string? Category = null,
    double? Price = null
);