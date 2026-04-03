namespace HomeStoq.Contracts;

public record ChatHistoryMessage
{
    public string Role { get; init; } = "";
    public string Text { get; init; } = "";
    
    public ChatHistoryMessage() { }
    
    public ChatHistoryMessage(string role, string text)
    {
        Role = role;
        Text = text;
    }
}