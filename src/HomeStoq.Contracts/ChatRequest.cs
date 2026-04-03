using System.Text.Json.Serialization;

namespace HomeStoq.Contracts;

public class ChatRequest
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
    
    [JsonPropertyName("history")]
    public List<ChatHistoryMessage>? History { get; set; }
}