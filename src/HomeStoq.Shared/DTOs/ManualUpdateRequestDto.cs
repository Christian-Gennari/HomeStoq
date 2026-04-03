namespace HomeStoq.Shared.DTOs;

public record ManualUpdateRequestDto(
    string ItemName,
    double QuantityChange,
    double? Price,
    string? Currency
);