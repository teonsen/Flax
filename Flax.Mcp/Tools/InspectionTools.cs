using System;
using System.Collections.Generic;
using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Flax.Mcp.Tools;

[McpServerToolType]
public static class InspectionTools
{
    [McpServerTool, Description("Return the window's UIA element tree as token-efficient JSON. Each node has a sequential 'id' valid only in this snapshot; pass it to click(elementId). Re-call each turn.")]
    public static string GetElementTree(SessionManager sessions, string sessionId, int maxDepth = -1, bool includeOffscreen = false) => ToolRunner.Run(() =>
    {
        if (!sessions.TryGet(sessionId, out var window))
            return Json.Of(new { ok = false, error = "session_not_found", hint = "Call open_window first." });

        var json = window.GetElementTreeAsJson(maxDepth, includeOffscreen);
        return json ?? Json.Of(new { ok = false, error = "tree_unavailable", hint = "Root not accessible (e.g. WinUI3). Use capture_window + Vision instead." });
    });

    [McpServerTool, Description("Find a single element by its accessible name and register it in the snapshot. Returns its id, rect [x,y,w,h] and center [x,y]. Use the id with click, or the center coordinates as a fallback.")]
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
                return new ContentBlock[] { new TextContentBlock { Text = Json.Of(new { ok = false, error = "session_not_found" }) } };

            window.Activate();
            var png = window.CaptureToPngBytes();
            if (png == null || png.Length == 0)
                return new ContentBlock[] { new TextContentBlock { Text = Json.Of(new { ok = false, error = "capture_failed" }) } };

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
}
