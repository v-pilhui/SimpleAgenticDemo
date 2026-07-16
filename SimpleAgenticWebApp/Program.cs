using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SimpleAgenticWebApp;
using SimpleAgenticWebApp.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<CalculatorMcpClient>();

builder.Services.AddHttpClient("openai", client =>
{
    client.BaseAddress = new Uri("https://api.openai.com/");
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/api/chat", async (
    ChatRequest request,
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    CalculatorMcpClient calculatorMcpClient
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

    var http = httpClientFactory.CreateClient("openai");
    http.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", apiKey);

    var messages = new List<object>
    {
        new
        {
            role = "system",
            content = """
            You are a helpful agent inside an ASP.NET Core web app.
            You can call tools when useful.
            If a tool result is needed, call the tool first.
            Keep answers short, clear, and practical.
            """
        }
    };

    foreach (var msg in request.History.TakeLast(10))
    {
        messages.Add(new
        {
            role = msg.Role,
            content = msg.Content
        });
    }

    var tools = new List<object>
    {
        new
        {
            type = "function",
            function = new
            {
                name = "get_current_time",
                description = "Get the current server time.",
                parameters = new
                {
                    type = "object",
                    properties = new { },
                    required = Array.Empty<string>()
                }
            }
        }
    };

    tools.AddRange((await calculatorMcpClient.ListToolNamesAsync()).Select(tool => new
    {
        type = "function",
        function = new
        {
            name = tool.Name,
            description = tool.Description,
            parameters = tool.JsonSchema
        }
    }));

    for (var step = 0; step < 4; step++)
    {
        var payload = new
        {
            model,
            messages,
            tools,
            tool_choice = "auto"
        };

        using var response = await http.PostAsync(
            "v1/chat/completions",
            new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json"));

        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return Results.Problem(json);
        }

        using var doc = JsonDocument.Parse(json);
        var message = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message");

        if (message.TryGetProperty("tool_calls", out var toolCalls))
        {
            messages.Add(JsonSerializer.Deserialize<object>(message.GetRawText())!);

            foreach (var toolCall in toolCalls.EnumerateArray())
            {
                var toolCallId = toolCall.GetProperty("id").GetString()!;
                var function = toolCall.GetProperty("function");
                var functionName = function.GetProperty("name").GetString()!;
                var argumentsJson = function.GetProperty("arguments").GetString() ?? "{}";

                var toolResult = await RunToolAsync(
                    functionName,
                    argumentsJson,
                    calculatorMcpClient,
                    CancellationToken.None);

                messages.Add(new
                {
                    role = "tool",
                    tool_call_id = toolCallId,
                    content = toolResult
                });
            }

            continue;
        }

        var finalAnswer = message.GetProperty("content").GetString();

        return Results.Ok(new ChatResponse
        {
            Reply = finalAnswer ?? ""
        });
    }

    return Results.Ok(new ChatResponse
    {
        Reply = "I tried to use tools, but the agent loop reached its limit."
    });
});

app.Run();

static async Task<string> RunToolAsync(
    string functionName,
    string argumentsJson,
    CalculatorMcpClient calculatorMcpClient,
    CancellationToken cancellationToken)
{
    return functionName switch
    {
        "get_current_time" =>
            DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"),

        "calculate" =>
            await calculatorMcpClient.CalculateAsync(argumentsJson, cancellationToken),

        _ =>
            $"Unknown tool: {functionName}"
    };
}
