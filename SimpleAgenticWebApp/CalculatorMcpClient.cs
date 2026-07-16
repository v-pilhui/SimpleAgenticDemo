using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace SimpleAgenticWebApp;

public sealed class CalculatorMcpClient : IAsyncDisposable
{
    private readonly Task<McpClient> _clientTask;

    public CalculatorMcpClient(IConfiguration config)
    {
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(config["MCPServers:Calculator:Url"] ?? "http://localhost:3001"),
            TransportMode = HttpTransportMode.AutoDetect
        });

        _clientTask = McpClient.CreateAsync(transport);
    }

    public async Task<IList<McpClientTool>> ListToolNamesAsync()
    {
        var client = await _clientTask;
        var tools = await client.ListToolsAsync();

        return tools;

        //var openAiTools = tools.Select(tool => new
        //{
        //    type = "function",
        //    function = new
        //    {
        //        name = tool.Name,
        //        description = tool.Description,
        //        parameters = tool.JsonSchema
        //    }
        //});

        //return tools
        //    .Select(t => t.Name)
        //    .ToList();
    }

    public async Task<string> CalculateAsync(
        string argumentsJson,
        CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(argumentsJson);

        var expression = doc.RootElement
            .GetProperty("expression")
            .GetString() ?? "";

        var client = await _clientTask;

        var tools = await client.ListToolsAsync();

        var calculateTool = tools.FirstOrDefault(t =>
            string.Equals(t.Name, "calculate", StringComparison.OrdinalIgnoreCase));

        if (calculateTool is null)
        {
            return "Calculator MCP tool was not found.";
        }

        var result = await client.CallToolAsync(
            calculateTool.Name,
            new Dictionary<string, object?>
            {
                ["expression"] = expression
            },
            cancellationToken: cancellationToken);

        var text = string.Join(
            "\n",
            result.Content
                .OfType<TextContentBlock>()
                .Select(c => c.Text));

        return string.IsNullOrWhiteSpace(text)
            ? "Calculator MCP tool returned no text."
            : text;
    }

    public async ValueTask DisposeAsync()
    {
        if (_clientTask.IsCompletedSuccessfully)
        {
            await _clientTask.Result.DisposeAsync();
        }
    }
}
