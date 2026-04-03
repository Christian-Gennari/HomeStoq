using System.Text.Json.Serialization;

namespace HomeStoq.Contracts;

public record ChatResponse
{
    [JsonPropertyName("reply")]
    public string Reply { get; init; } = "";
    
    [JsonPropertyName("history")]
    public List<ChatHistoryMessage> History { get; init; } = new();
}