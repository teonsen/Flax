using System;

namespace Flax.Mcp;

/// <summary>
/// Wraps a tool body so any unexpected exception (e.g. a stale FlaUI/COM handle) is returned to the
/// LLM as a structured { ok: false, error: "unexpected_error", message } response instead of escaping
/// the MCP dispatch loop.
/// </summary>
internal static class ToolRunner
{
    public static string Run(Func<string> body)
    {
        try
        {
            return body();
        }
        catch (Exception ex)
        {
            return Json.Of(new { ok = false, error = "unexpected_error", message = ex.Message });
        }
    }
}
