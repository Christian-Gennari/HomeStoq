namespace HomeStoq.Contracts;

public class ParsedVoiceAction
{
    public string ItemName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public string? Category { get; set; }
}
