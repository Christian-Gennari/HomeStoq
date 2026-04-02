using Microsoft.Extensions.AI;

namespace HomeStoq.Contracts;

public record ChatResponse(string Reply, List<ChatMessage> History);
