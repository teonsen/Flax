using System.ComponentModel;
using System.Linq;
using Flax;
using ModelContextProtocol.Server;

namespace Flax.Mcp.Tools;

[McpServerToolType]
public static class WindowTools
{
    [McpServerTool, Description("Launch an application by path or name (e.g. 'notepad.exe', 'calc.exe').")]
    public static string LaunchApp(WindowsAutomation automation, string path, string args = "")
    {
        try
        {
            automation.Process.Run(path, args);
            return Json.Of(new { ok = true, message = $"Launched {path}." });
        }
        catch (System.Exception ex)
        {
            return Json.Of(new { ok = false, error = "launch_failed", message = ex.Message });
        }
    }

    [McpServerTool, Description("List visible top-level windows with title, pid, className, rect [x,y,w,h], minimized.")]
    public static string ListWindows()
    {
        var windows = WindowsAutomation.GetWindowList().Select(w => new
        {
            title = w.Title,
            pid = w.PID,
            className = w.ClassName,
            rect = new[] { w.Left, w.Top, w.Width, w.Height },
            minimized = w.IsMinimized
        });
        return Json.Of(new { ok = true, windows });
    }

    [McpServerTool, Description("Open a window by title (supports % wildcards: '%text%', '%suffix', 'prefix%') and start a UIA session. Returns a sessionId used by other tools.")]
    public static string OpenWindow(WindowsAutomation automation, SessionManager sessions, string titleQuery, int timeoutSec = 10)
    {
        var window = automation.GetWindow(titleQuery, timeoutSec);
        if (window == null)
            return Json.Of(new { ok = false, error = "window_not_found", hint = "Check the title or call list_windows." });

        var sessionId = sessions.Open(window);
        return Json.Of(new
        {
            ok = true,
            sessionId,
            title = window.Title,
            rect = new[] { window.Left, window.Top, window.Width, window.Height }
        });
    }

    [McpServerTool, Description("Bring the session's window to the foreground.")]
    public static string ActivateWindow(SessionManager sessions, string sessionId)
    {
        if (!sessions.TryGet(sessionId, out var window))
            return Json.Of(new { ok = false, error = "session_not_found", hint = "Call open_window first." });
        window.Activate();
        return Json.Of(new { ok = true });
    }

    [McpServerTool, Description("Close the session's window and release the UIA session.")]
    public static string CloseWindow(SessionManager sessions, string sessionId)
    {
        if (!sessions.TryGet(sessionId, out var window))
            return Json.Of(new { ok = false, error = "session_not_found", hint = "Call open_window first." });
        window.Close();
        sessions.Remove(sessionId);
        return Json.Of(new { ok = true });
    }
}
