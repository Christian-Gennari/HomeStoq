namespace HomeStoq.Contracts;

public class PantryItem
{
    public string ReceiptText { get; set; } = string.Empty;
    public string ExpandedName { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public string? Category { get; set; }
    public double? Price { get; set; }
}
