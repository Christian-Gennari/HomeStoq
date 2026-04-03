using System.Text.Json.Serialization;

namespace HomeStoq.Contracts;

public record ChatRequestDto
{
    [JsonPropertyName("message")]
    public string Message { get; init; } = "";
    
    [JsonPropertyName("history")]
    public List<ChatHistoryMessageDto>? History { get; init; }
}