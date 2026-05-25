using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using Flax.Mcp.Llm;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Flax.Mcp.Tools;

[McpServerToolType]
public static class InspectionTools
{
    [McpServerTool, Description("Return the window's UIA element tree under { ok:true, tree:<nodes> }. Each node has a sequential 'id' valid only in this snapshot; pass it to click(elementId). Re-call each turn.")]
    public static string GetElementTree(SessionManager sessions, string sessionId, int maxDepth = -1, bool includeOffscreen = false) => ToolRunner.Run(() =>
    {
        if (!sessions.TryGet(sessionId, out var window))
            return Json.Of(new { ok = false, error = "session_not_found", hint = "Call open_window first." });

        var json = window.GetElementTreeAsJson(maxDepth, includeOffscreen);
        if (json == null)
            return Json.Of(new { ok = false, error = "tree_unavailable", hint = "Root not accessible (e.g. WinUI3). Use capture_window + Vision instead." });

        return Json.Of(new { ok = true, tree = System.Text.Json.Nodes.JsonNode.Parse(json) });
    });

    [McpServerTool, Description("Find a single element by its accessible name and register it in the snapshot. Returns its id, rect [x,y,w,h] and center [x,y]. The id is valid only until the next get_element_tree call rebuilds the snapshot. Use the id with click, or the center coordinates as a fallback.")]
    public static string FindElement(SessionManager sessions, string sessionId, string name) => ToolRunner.Run(() =>
    {
        if (!sessions.TryGet(sessionId, out var window))
            return Json.Of(new { ok = false, error = "session_not_found", hint = "Call open_window first." });

        var element = window.GetElementByName(name);
        if (element == null)
            return Json.Of(new { ok = false, error = "element_not_found", hint = "Try get_element_tree or capture_window." });

        var id = window.RegisterFoundElement(element);
        var r = element.BoundingRectangle;
        return Json.Of(new
        {
            ok = true,
            id,
            name = element.Name,
            rect = new[] { r.X, r.Y, r.Width, r.Height },
            center = new[] { element.CenterX, element.CenterY }
        });
    });

    [McpServerTool, Description("Capture a screenshot of the session's window as PNG (for Vision when the UIA tree is too shallow). Image pixel (0,0) maps to screen coordinate windowOrigin [x,y]; add windowOrigin to the pixel you pick before calling click(x,y).")]
    public static IEnumerable<ContentBlock> CaptureWindow(SessionManager sessions, string sessionId)
    {
        try
        {
            if (!sessions.TryGet(sessionId, out var window))
                return new ContentBlock[] { new TextContentBlock { Text = Json.Of(new { ok = false, error = "session_not_found", hint = "Call open_window first." }) } };

            window.Activate();
            var png = window.CaptureToPngBytes();
            if (png == null || png.Length == 0)
                return new ContentBlock[] { new TextContentBlock { Text = Json.Of(new { ok = false, error = "capture_failed", hint = "Ensure the window is visible and not minimized." }) } };

            return new ContentBlock[]
            {
                new TextContentBlock
                {
                    Text = Json.Of(new
                    {
                        ok = true,
                        message = "Window screenshot. To click a pixel (px,py) in this image, call click with x=windowOrigin[0]+px, y=windowOrigin[1]+py.",
                        windowOrigin = new[] { window.Left, window.Top }
                    })
                },
                ImageContentBlock.FromBytes(png, "image/png")
            };
        }
        catch (Exception ex)
        {
            return new ContentBlock[] { new TextContentBlock { Text = Json.Of(new { ok = false, error = "unexpected_error", message = ex.Message }) } };
        }
    }

    [McpServerTool, Description("Locate one UI element from a natural-language target using the server-side locator model (a cheap model configured via FLAX_LLM_*). Returns a small result you can pass straight to click: tree mode -> { elementId }, vision mode -> { x, y } in absolute screen coordinates. mode: auto (default, tree then vision) | tree | vision.")]
    public static string LocateElement(SessionManager sessions, ElementLocator locator, string sessionId, string target, string? mode = null) => ToolRunner.Run(() =>
    {
        if (!sessions.TryGet(sessionId, out var window))
            return Json.Of(new { ok = false, error = "session_not_found", hint = "Call open_window first." });

        var outcome = new LocateService()
            .LocateAsync(locator, new FlaxWindowLocateAdapter(window), target, LocateModeParser.Parse(mode), CancellationToken.None)
            .GetAwaiter().GetResult();

        return outcome.Ok
            ? Json.Of(new { ok = true, mode = outcome.Mode, elementId = outcome.ElementId, x = outcome.X, y = outcome.Y, confidence = outcome.Confidence, reasoning = outcome.Reasoning })
            : Json.Of(new { ok = false, error = outcome.Error, hint = outcome.Hint, reasoning = outcome.Reasoning });
    });
}
