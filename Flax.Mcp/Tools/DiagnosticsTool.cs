using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Flax.Mcp.Tools;

[McpServerToolType]
public static class DiagnosticsTool
{
    [McpServerTool, Description("Health check. Returns 'pong'.")]
    public static string Ping() => "pong";
}
