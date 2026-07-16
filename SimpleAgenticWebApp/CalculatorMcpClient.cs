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

    public async Task<IList<McpClientTool>> ListToolsAsync(CancellationToken cancellationToken)
    {
        var client = await _clientTask;
        var tools = await client.ListToolsAsync(cancellationToken: cancellationToken);

        return tools;
    }

    public async ValueTask DisposeAsync()
    {
        if (_clientTask.IsCompletedSuccessfully)
        {
            await _clientTask.Result.DisposeAsync();
        }
    }
}
