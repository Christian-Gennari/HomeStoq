namespace HomeStoq.Contracts;

public class Receipt
{
    public long Id { get; set; }
    public string Timestamp { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public double TotalAmountPaid { get; set; }
}
