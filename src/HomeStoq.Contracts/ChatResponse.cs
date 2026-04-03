using System.Text.Json.Serialization;

namespace HomeStoq.Contracts;

public class ChatResponse
{
    [JsonPropertyName("reply")]
    public string Reply { get; set; } = "";
    
    [JsonPropertyName("history")]
    public List<ChatHistoryMessage> History { get; set; } = new();
}