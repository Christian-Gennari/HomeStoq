using Microsoft.Extensions.AI;

namespace HomeStoq.Contracts;

public record ChatRequest(string Message, List<ChatMessage>? History = null);
