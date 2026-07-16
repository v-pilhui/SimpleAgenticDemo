namespace SimpleAgenticWebApp.Models;

public sealed class ChatRequest
{
    public List<ClientMessage> History { get; set; } = [];
}
