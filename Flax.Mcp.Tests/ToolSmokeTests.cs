using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Flax;
using Flax.Mcp.Tools;
using ModelContextProtocol.Protocol;
using NUnit.Framework;

namespace Flax.Mcp.Tests;

[Explicit("Launches mspaint; run interactively on a Windows desktop.")]
public class ToolSmokeTests
{
    private static string SessionIdFrom(string openWindowJson)
    {
        using var doc = JsonDocument.Parse(openWindowJson);
        Assert.That(doc.RootElement.GetProperty("ok").GetBoolean(), Is.True, openWindowJson);
        return doc.RootElement.GetProperty("sessionId").GetString()!;
    }

    [Test]
    public void Launch_Open_Tree_Capture_Close_RoundTrip()
    {
        var automation = new WindowsAutomation();
        var sessions = new SessionManager();

        WindowTools.LaunchApp(automation, "mspaint.exe");
        Thread.Sleep(2000);

        var sessionId = SessionIdFrom(WindowTools.OpenWindow(automation, sessions, "%Paint%", 10));

        var tree = InspectionTools.GetElementTree(sessions, sessionId);
        Assert.That(tree, Does.Contain("controlType"));

        var capture = InspectionTools.CaptureWindow(sessions, sessionId).ToList();
        Assert.That(capture.OfType<ImageContentBlock>().Any(), Is.True);

        var closed = WindowTools.CloseWindow(sessions, sessionId);
        using var doc = JsonDocument.Parse(closed);
        Assert.That(doc.RootElement.GetProperty("ok").GetBoolean(), Is.True);
    }
}
