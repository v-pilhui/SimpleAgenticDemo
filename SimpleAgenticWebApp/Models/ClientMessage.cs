namespace SimpleAgenticWebApp.Models;

public sealed class ClientMessage
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = "";
}
