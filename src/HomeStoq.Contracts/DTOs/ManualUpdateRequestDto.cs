namespace HomeStoq.Contracts;

public record ManualUpdateRequestDto(
    string ItemName,
    double QuantityChange,
    double? Price,
    string? Currency
);