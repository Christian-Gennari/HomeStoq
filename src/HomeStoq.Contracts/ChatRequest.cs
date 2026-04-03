using System.Text.Json.Serialization;

namespace HomeStoq.Contracts;

public record ChatRequest
{
    [JsonPropertyName("message")]
    public string Message { get; init; } = "";
    
    [JsonPropertyName("history")]
    public List<ChatHistoryMessage>? History { get; init; }
}