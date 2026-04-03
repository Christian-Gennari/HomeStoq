namespace HomeStoq.Contracts;

public record ChatHistoryMessageDto
{
    public string Role { get; init; } = "";
    public string Text { get; init; } = "";
    
    public ChatHistoryMessageDto() { }
    
    public ChatHistoryMessageDto(string role, string text)
    {
        Role = role;
        Text = text;
    }
}