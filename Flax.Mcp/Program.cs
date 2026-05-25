using Flax;
using Flax.Mcp;
using Flax.Mcp.Llm;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// stdio transport uses stdout for protocol messages only; route all logs to stderr.
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddSingleton<WindowsAutomation>();
builder.Services.AddSingleton<SessionManager>();

// Server-side element locator (cheap, independently-configured model). Always registered; the tool
// branches on Status so the MCP DI injection never breaks when the LLM is unconfigured.
var llm = LlmOptions.Resolve(builder.Configuration, Environment.GetEnvironmentVariable);
var apiKey = string.IsNullOrEmpty(llm.ApiKeyEnvVar) ? null : Environment.GetEnvironmentVariable(llm.ApiKeyEnvVar);
var locatorStatus = llm.GetStatus(apiKey);
IChatClient? chatClient = locatorStatus == LocatorStatus.Ready ? ChatClientFactory.Create(llm, apiKey!) : null;
builder.Services.AddSingleton(new ElementLocator(chatClient, locatorStatus, llm.MaxOutputTokens, llm.Model));

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
