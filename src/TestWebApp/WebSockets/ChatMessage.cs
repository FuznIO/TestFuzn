namespace TestWebApp.WebSockets;

public class ChatMessage
{
    public string Type { get; set; }
    public string Content { get; set; }
    public DateTime Timestamp { get; set; }
}
