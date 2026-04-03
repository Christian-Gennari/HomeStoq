using System;

namespace HomeStoq.Shared.DTOs;

public record ReceiptDto
{
    public long Id { get; init; }
    public DateTime Timestamp { get; init; }
    public string StoreName { get; init; } = string.Empty;
    public double TotalAmountPaid { get; init; }
}
