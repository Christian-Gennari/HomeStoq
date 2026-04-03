using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace HomeStoq.Contracts;

public record ChatRequestDto(
    [property: JsonPropertyName("message")] string Message = "",
    [property: JsonPropertyName("history")] List<ChatHistoryMessageDto>? History = null
);