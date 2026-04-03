namespace HomeStoq.Contracts;

public record ChatResponse(string Reply, List<ChatHistoryMessage> History);
