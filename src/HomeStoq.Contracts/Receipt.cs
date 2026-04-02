namespace HomeStoq.Contracts;

public record Receipt(int Id, DateTime Timestamp, string StoreName, double TotalAmountPaid);
