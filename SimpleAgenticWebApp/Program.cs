using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using SimpleAgenticWebApp;
using SimpleAgenticWebApp.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<CalculatorMcpClient>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/api/chat", async (
    ChatRequest request,
    IConfiguration config,
    CalculatorMcpClient calculatorMcpClient,
    ILoggerFactory loggerFactory,
    IServiceProvider serviceProvider,
    CancellationToken cancellationToken
) =>
{
    var apiKey = config["OpenAI:ApiKey"];

    if (string.IsNullOrWhiteSpace(apiKey))
    {
        return Results.BadRequest(new
        {
            error = "OPENAI_API_KEY is missing."
        });
    }

    var model = config["OpenAI:Model"] ?? "gpt-5.1";
    IChatClient chatClient = new OpenAI.Chat.ChatClient(model, apiKey)
        .AsIChatClient()
        .AsBuilder()
        .UseKernelFunctionInvocation(loggerFactory)
        .Build(serviceProvider);

    var messages = new List<Microsoft.Extensions.AI.ChatMessage>
    {
        new(ChatRole.System, """
            You are a helpful agent inside an ASP.NET Core web app.
            You can call tools when useful.
            If a tool result is needed, call the tool first.
            Keep answers short, clear, and practical.
            """)
    };

    foreach (var msg in request.History.TakeLast(10))
    {
        messages.Add(new Microsoft.Extensions.AI.ChatMessage(MapRole(msg.Role), msg.Content));
    }

    var getCurrentTimeTool = AIFunctionFactory.Create(
        () => DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"),
        "get_current_time",
        "Get the current server time.");

    IList<AITool> tools = [getCurrentTimeTool, .. await calculatorMcpClient.ListToolsAsync(cancellationToken)];

    var response = await chatClient.GetResponseAsync(
        messages,
        new ChatOptions
        {
            Tools = tools
        },
        cancellationToken);

    return Results.Ok(new SimpleAgenticWebApp.Models.ChatResponse
    {
        Reply = response.Text ?? ""
    });
});

app.Run();

static ChatRole MapRole(string role)
{
    return role.ToLowerInvariant() switch
    {
        "assistant" => ChatRole.Assistant,
        "system" => ChatRole.System,
        "tool" => ChatRole.Tool,
        _ => ChatRole.User
    };
}
