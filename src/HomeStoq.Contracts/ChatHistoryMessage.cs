namespace HomeStoq.Contracts;

public class ChatHistoryMessage
{
    public string Role { get; set; } = "";
    public string Text { get; set; } = "";
    
    public ChatHistoryMessage() { }
    
    public ChatHistoryMessage(string role, string text)
    {
        Role = role;
        Text = text;
    }
}