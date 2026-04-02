namespace HomeStoq.Contracts;

public record ManualUpdateRequest(
    string ItemName,
    double QuantityChange,
    double? Price,
    string? Currency
);
