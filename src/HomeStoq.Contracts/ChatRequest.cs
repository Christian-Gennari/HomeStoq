using System.Text.Json.Serialization;

namespace HomeStoq.Contracts;

public record ChatRequest(
    [property: JsonPropertyName("message")] string Message, 
    [property: JsonPropertyName("history")] List<ChatHistoryMessage>? History = null
);
