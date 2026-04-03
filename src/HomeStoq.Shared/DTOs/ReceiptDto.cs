namespace HomeStoq.Shared.DTOs;

public record ReceiptDto(
    long Id = 0,
    string Timestamp = "",
    string StoreName = "",
    double TotalAmountPaid = 0
);