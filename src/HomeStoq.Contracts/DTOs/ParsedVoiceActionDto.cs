namespace HomeStoq.Contracts;

public record ParsedVoiceActionDto(
    string ItemName = "",
    string Action = "",
    double Quantity = 0,
    string? Category = null
);