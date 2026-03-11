using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Trading212McpServer;

var apiKey = Environment.GetEnvironmentVariable("T212_API_KEY");
var apiSecret = Environment.GetEnvironmentVariable("T212_API_SECRET");
var environment = Environment.GetEnvironmentVariable("T212_ENVIRONMENT") ?? "demo";

if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiSecret))
{
    Console.Error.WriteLine("ERROR: Missing required environment variables.");
    Console.Error.WriteLine("  T212_API_KEY     - Your Trading 212 API key");
    Console.Error.WriteLine("  T212_API_SECRET  - Your Trading 212 API secret");
    Console.Error.WriteLine("  T212_ENVIRONMENT - 'live' or 'demo' (default: demo)");
    Environment.Exit(1);
}

var baseUrl = environment.Equals("live", StringComparison.OrdinalIgnoreCase)
    ? "https://live.trading212.com/api/v0"
    : "https://demo.trading212.com/api/v0";

var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{apiKey}:{apiSecret}"));

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddHttpClient<Trading212Client>(client =>
{
    client.BaseAddress = new Uri(baseUrl + "/");
    client.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

// Initialize alert config from appsettings.json (cached for tool calls)
Trading212McpServer.Config.AlertConfigLoader.Load(builder.Configuration);

var app = builder.Build();
await app.RunAsync();
