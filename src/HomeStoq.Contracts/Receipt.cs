namespace HomeStoq.Contracts;

public record Receipt
{
    public long Id { get; init; }
    public string Timestamp { get; init; } = string.Empty;
    public string StoreName { get; init; } = string.Empty;
    public double TotalAmountPaid { get; init; }
}
