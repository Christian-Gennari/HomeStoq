namespace HomeStoq.Shared.DTOs;

public record PantryItemDto
{
    public string ReceiptText { get; init; } = string.Empty;
    public string ExpandedName { get; init; } = string.Empty;
    public string ItemName { get; init; } = string.Empty;
    public double Quantity { get; init; }
    public string? Category { get; init; }
    public double? Price { get; init; }
}
