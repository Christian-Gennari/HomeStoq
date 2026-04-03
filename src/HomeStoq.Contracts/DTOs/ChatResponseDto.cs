using System.Text.Json.Serialization;

namespace HomeStoq.Contracts;

public record ChatResponseDto
{
    [JsonPropertyName("reply")]
    public string Reply { get; init; } = "";
    
    [JsonPropertyName("history")]
    public List<ChatHistoryMessageDto> History { get; init; } = new();
}