namespace HomeStoq.Contracts;

public record ParsedVoiceActionDto
{
    public string ItemName { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public double Quantity { get; init; }
    public string? Category { get; init; }
}
