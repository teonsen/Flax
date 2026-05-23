using System;
using System.ComponentModel;
using Flax.Windows;
using ModelContextProtocol.Server;

namespace Flax.Mcp.Tools;

[McpServerToolType]
public static class ActionTools
{
    [McpServerTool, Description("Click an element by its snapshot id (UIA, with coordinate fallback) OR at absolute screen coordinates x,y. Provide either elementId or both x and y. button: 'left'|'right'. Set doubleClick=true for a double click.")]
    public static string Click(
        SessionManager sessions,
        string sessionId,
        int? elementId = null,
        int? x = null,
        int? y = null,
        string button = "left",
        bool doubleClick = false) => ToolRunner.Run(() =>
    {
        if (!sessions.TryGet(sessionId, out var window))
            return Json.Of(new { ok = false, error = "session_not_found", hint = "Call open_window first." });

        if (doubleClick && string.Equals(button, "right", StringComparison.OrdinalIgnoreCase))
            return Json.Of(new { ok = false, error = "unsupported_combination", hint = "Right-button double-click is not supported. Use a single right click or a left double-click." });

        var kind = doubleClick
            ? ClickKind.LeftDouble
            : (string.Equals(button, "right", StringComparison.OrdinalIgnoreCase) ? ClickKind.Right : ClickKind.Left);

        window.Activate();
        var mouse = new FlaxMouse();

        var outcome = new ClickService().Click(
            new FlaxWindowElementLookup(window),
            elementId, x, y, kind,
            (cx, cy, k) =>
            {
                switch (k)
                {
                    case ClickKind.LeftDouble: mouse.DoubleClick(cx, cy); break;
                    case ClickKind.Right: mouse.RightClick(cx, cy); break;
                    default: mouse.Click(cx, cy); break;
                }
            });

        if (outcome.Success)
            return Json.Of(new { ok = true, method = outcome.Method, uiaError = outcome.UiaError });

        return Json.Of(new
        {
            ok = false,
            error = outcome.Error,
            hint = outcome.Error == "element_not_found" ? "Re-run get_element_tree for a fresh snapshot." : null
        });
    });

    [McpServerTool, Description("Type literal text into the session's focused control.")]
    public static string TypeText(SessionManager sessions, string sessionId, string text) => ToolRunner.Run(() =>
    {
        if (!sessions.TryGet(sessionId, out var window))
            return Json.Of(new { ok = false, error = "session_not_found", hint = "Call open_window first." });
        window.Activate();
        new FlaxKeyboard().Type(text);
        return Json.Of(new { ok = true });
    });

    [McpServerTool, Description("Press a special key or combo. Supported: ENTER, ESC, TAB, SPACE, BACKSPACE, DELETE, UP, DOWN, LEFT, RIGHT, CTRL+A, CTRL+C, CTRL+V.")]
    public static string SendKeys(SessionManager sessions, string sessionId, string keys) => ToolRunner.Run(() =>
    {
        if (!sessions.TryGet(sessionId, out var window))
            return Json.Of(new { ok = false, error = "session_not_found", hint = "Call open_window first." });
        if (!SendKeysMap.TryGet(keys, out var action))
            return Json.Of(new { ok = false, error = "unknown_key", hint = "Use type_text for arbitrary text." });
        window.Activate();
        action(new FlaxKeyboard());
        return Json.Of(new { ok = true });
    });

    [McpServerTool, Description("Scroll the session's window. Positive lines scroll up/right; negative down/left. Set horizontal=true for horizontal scroll.")]
    public static string Scroll(SessionManager sessions, string sessionId, double lines, bool horizontal = false) => ToolRunner.Run(() =>
    {
        if (!sessions.TryGet(sessionId, out var window))
            return Json.Of(new { ok = false, error = "session_not_found", hint = "Call open_window first." });
        window.Activate();
        if (horizontal) FlaxMouse.HorizontalScroll(lines);
        else FlaxMouse.VerticalScroll(lines);
        return Json.Of(new { ok = true });
    });
}
