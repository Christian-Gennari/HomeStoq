using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace HomeStoq.Shared.DTOs;

public record ChatResponseDto(
    [property: JsonPropertyName("reply")] string Reply = "",
    [property: JsonPropertyName("history")] List<ChatHistoryMessageDto>? History = null
);