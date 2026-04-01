namespace HomeStoq.Server.Models;

public class InventoryItem
{
    public int Id { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public string? Category { get; set; }
    public double? LastPrice { get; set; }
    public string? Currency { get; set; }
    public DateTime UpdatedAt { get; set; }
}
