namespace HomeStoq.Contracts;

public record ReceiptDto
{
    public long Id { get; init; }
    public string Timestamp { get; init; } = string.Empty;
    public string StoreName { get; init; } = string.Empty;
    public double TotalAmountPaid { get; init; }
}
