# Flax MCP Server Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Expose Flax's UI automation as an MCP server (`Flax.Mcp`) so an LLM client (Claude Desktop / Claude Code) can launch a Windows app, read its UI element tree, and click elements — with a screenshot+Vision fallback for WinUI3 apps.

**Architecture:** A new net8.0-windows console (`Flax.Mcp`) hosts the official `ModelContextProtocol` C# SDK over stdio. It references the existing `Flax` library, which is multi-targeted to `net472;net8.0-windows`. A singleton `SessionManager` keeps `FlaxWindow` instances alive across stateless tool calls, keyed by a `sessionId`. Clicking is ID-priority (UIA `Invoke`) with a coordinate fallback; a `capture_window` tool returns a PNG so a Vision model can pick pixel coordinates when the UIA tree is too shallow.

**Tech Stack:** C#, .NET 8 (`net8.0-windows`) + .NET Framework 4.7.2, FlaUI 3.0 (UIA3), `ModelContextProtocol` SDK, NUnit 4.x, System.Drawing.

---

## File Structure

| File | Responsibility |
|---|---|
| `Flax/Flax.csproj` (modify) | Multi-target `net472;net8.0-windows` |
| `Flax/Windows/FlaxWindow.cs` (modify) | Add `CaptureToPngBytes()`, `RegisterFoundElement()` |
| `Flax.Mcp/Flax.Mcp.csproj` (create) | net8.0-windows console, MCP SDK + Flax reference |
| `Flax.Mcp/Program.cs` (create) | Host build, DI registration, stdio transport |
| `Flax.Mcp/Json.cs` (create) | Null-omitting JSON serializer helper for tool responses |
| `Flax.Mcp/SessionManager.cs` (create) | sessionId → FlaxWindow registry |
| `Flax.Mcp/ClickService.cs` (create) | ID-priority/coordinate-fallback click decision logic + adapters |
| `Flax.Mcp/SendKeysMap.cs` (create) | Key-name → FlaxKeyboard action map |
| `Flax.Mcp/Tools/DiagnosticsTool.cs` (create) | `ping` health check |
| `Flax.Mcp/Tools/WindowTools.cs` (create) | launch_app / list_windows / open_window / activate_window / close_window |
| `Flax.Mcp/Tools/InspectionTools.cs` (create) | get_element_tree / find_element / capture_window |
| `Flax.Mcp/Tools/ActionTools.cs` (create) | click / type_text / send_keys / scroll |
| `Flax.Mcp.Tests/Flax.Mcp.Tests.csproj` (create) | net8.0-windows NUnit test project |
| `Flax.Mcp.Tests/SessionManagerTests.cs` (create) | Unit tests for SessionManager |
| `Flax.Mcp.Tests/ClickServiceTests.cs` (create) | Unit tests for click dispatch |
| `Flax.Mcp.Tests/SendKeysMapTests.cs` (create) | Unit tests for key mapping |
| `Flax.Mcp.Tests/ToolSmokeTests.cs` (create) | Explicit integration smoke against mspaint |

**Testability note:** UIA-bound code cannot be unit tested without a live window. The pure, testable cores (`SessionManager`, `ClickService`, `SendKeysMap`) get TDD unit tests. UIA-touching tools are verified by the explicit integration smoke (Task 7) and manual E2E (Task 8).

---

## Task 1: Multi-target Flax and add library helpers

**Files:**
- Modify: `Flax/Flax.csproj`
- Modify: `Flax/Windows/FlaxWindow.cs`

- [ ] **Step 1: Multi-target the csproj**

Replace `Flax/Flax.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFrameworks>net472;net8.0-windows</TargetFrameworks>
    <RootNamespace>Flax</RootNamespace>
    <AssemblyName>Flax</AssemblyName>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
    <AutoGenerateBindingRedirects>true</AutoGenerateBindingRedirects>
    <UseWindowsForms>true</UseWindowsForms>
    <UseWPF>true</UseWPF>
  </PropertyGroup>

  <ItemGroup Condition="'$(TargetFramework)' == 'net472'">
    <Reference Include="Accessibility" />
    <Reference Include="PresentationCore" />
    <Reference Include="PresentationFramework" />
    <Reference Include="WindowsBase" />
    <Reference Include="System.Windows.Forms" />
    <Reference Include="System.IO.Compression" />
    <Reference Include="System.Drawing" />
  </ItemGroup>

  <ItemGroup Condition="'$(TargetFramework)' == 'net8.0-windows'">
    <PackageReference Include="System.Drawing.Common" Version="8.0.10" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="FlaUI.Core" Version="3.0.0" />
    <PackageReference Include="FlaUI.UIA3" Version="3.0.0" />
    <PackageReference Include="Interop.UIAutomationClient" Version="10.18362.0" />
    <PackageReference Include="System.Management" Version="4.7.0" />
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Build both target frameworks to verify they resolve**

Run: `dotnet build Flax/Flax.csproj`
Expected: PASS for both `net472` and `net8.0-windows`.

If `net8.0-windows` fails on `Properties/AssemblyInfo.cs` attribute conflicts (because `GenerateAssemblyInfo=false` shares it across TFMs), resolve by guarding any duplicated/auto-generated attributes; the WinForms/WPF theme attributes from net472 are the usual culprit. Re-run until both TFMs build.

- [ ] **Step 3: Add `using` directives to FlaxWindow.cs**

In `Flax/Windows/FlaxWindow.cs`, add to the top usings block:

```csharp
using System.IO;
using System.Drawing.Imaging;
```

- [ ] **Step 4: Add `CaptureToPngBytes` and `RegisterFoundElement` to FlaxWindow**

In `Flax/Windows/FlaxWindow.cs`, add these methods inside the `FlaxWindow` class (e.g. right after `Capture(string savePath)`):

```csharp
/// <summary>
/// Captures this window and returns the image as PNG bytes (for MCP image content / Vision).
/// Returns null if the capture failed.
/// </summary>
public byte[] CaptureToPngBytes()
{
    using (var img = Windows.Capture.Window(hWnd))
    {
        if (img == null) return null;
        using (var ms = new MemoryStream())
        {
            img.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }
    }
}

/// <summary>
/// Registers an element found outside a full tree walk (e.g. via GetElementByName) into the
/// current snapshot map and returns the assigned id, so it can be acted on with GetElementById.
/// The id is valid only until the next GetElementTreeAsJson call rebuilds the map.
/// </summary>
public int RegisterFoundElement(UIElement element)
{
    if (_elementMap == null) _elementMap = new Dictionary<int, UIElement>();
    int id = _elementMap.Count > 0 ? _elementMap.Keys.Max() + 1 : 0;
    element.Id = id;
    _elementMap[id] = element;
    return id;
}
```

- [ ] **Step 5: Rebuild to confirm the library still compiles on both TFMs**

Run: `dotnet build Flax/Flax.csproj`
Expected: PASS for both TFMs.

- [ ] **Step 6: Commit**

```bash
git add Flax/Flax.csproj Flax/Windows/FlaxWindow.cs
git commit -m "feat: multi-target Flax to net8.0-windows; add CaptureToPngBytes and RegisterFoundElement"
```

---

## Task 2: Scaffold the Flax.Mcp server and test project

**Files:**
- Create: `Flax.Mcp/Flax.Mcp.csproj`
- Create: `Flax.Mcp/Program.cs`
- Create: `Flax.Mcp/Tools/DiagnosticsTool.cs`
- Create: `Flax.Mcp.Tests/Flax.Mcp.Tests.csproj`
- Modify: `Flax.sln`

- [ ] **Step 1: Create the server project file**

Create `Flax.Mcp/Flax.Mcp.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>Flax.Mcp</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.1" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Flax\Flax.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Add the MCP SDK package (resolves the latest preview version)**

Run: `dotnet add Flax.Mcp/Flax.Mcp.csproj package ModelContextProtocol --prerelease`
Expected: package added; `dotnet restore` succeeds.

- [ ] **Step 3: Create the diagnostics tool**

Create `Flax.Mcp/Tools/DiagnosticsTool.cs`:

```csharp
using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Flax.Mcp.Tools;

[McpServerToolType]
public static class DiagnosticsTool
{
    [McpServerTool, Description("Health check. Returns 'pong'.")]
    public static string Ping() => "pong";
}
```

- [ ] **Step 4: Create Program.cs**

Create `Flax.Mcp/Program.cs` (the `SessionManager` registration is added in Task 3, once that type exists):

```csharp
using Flax;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// stdio transport uses stdout for protocol messages only; route all logs to stderr.
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddSingleton<WindowsAutomation>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
```

- [ ] **Step 5: Build the server to verify it compiles and discovers the ping tool**

Run: `dotnet build Flax.Mcp/Flax.Mcp.csproj`
Expected: PASS. (Runtime verification of the stdio handshake happens via Claude Desktop in Task 8.)

- [ ] **Step 6: Create the test project file**

Create `Flax.Mcp.Tests/Flax.Mcp.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="NUnit" Version="4.2.2" />
    <PackageReference Include="NUnit3TestAdapter" Version="4.6.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Flax.Mcp\Flax.Mcp.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 7: Add both projects to the solution**

Run: `dotnet sln Flax.sln add Flax.Mcp/Flax.Mcp.csproj Flax.Mcp.Tests/Flax.Mcp.Tests.csproj`
Expected: both projects added to `Flax.sln`.

- [ ] **Step 8: Commit**

```bash
git add Flax.Mcp/Flax.Mcp.csproj Flax.Mcp/Program.cs Flax.Mcp/Tools/DiagnosticsTool.cs Flax.Mcp.Tests/Flax.Mcp.Tests.csproj Flax.sln
git commit -m "chore: scaffold Flax.Mcp server and test projects"
```

---

## Task 3: SessionManager (TDD)

**Files:**
- Create: `Flax.Mcp/SessionManager.cs`
- Test: `Flax.Mcp.Tests/SessionManagerTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `Flax.Mcp.Tests/SessionManagerTests.cs`:

```csharp
using System;
using Flax.Mcp;
using Flax.Windows;
using NUnit.Framework;

namespace Flax.Mcp.Tests;

public class SessionManagerTests
{
    private static FlaxWindow NewWindow() => new FlaxWindow(IntPtr.Zero, 0);

    [Test]
    public void Open_ReturnsNonEmptyId_AndTryGetFindsWindow()
    {
        var sm = new SessionManager();
        var window = NewWindow();

        var id = sm.Open(window);

        Assert.That(id, Is.Not.Null.And.Not.Empty);
        Assert.That(sm.TryGet(id, out var found), Is.True);
        Assert.That(found, Is.SameAs(window));
    }

    [Test]
    public void Open_IssuesDistinctIds()
    {
        var sm = new SessionManager();
        var id1 = sm.Open(NewWindow());
        var id2 = sm.Open(NewWindow());
        Assert.That(id1, Is.Not.EqualTo(id2));
    }

    [Test]
    public void TryGet_UnknownId_ReturnsFalse()
    {
        var sm = new SessionManager();
        Assert.That(sm.TryGet("nope", out _), Is.False);
    }

    [Test]
    public void Remove_ExistingId_ReturnsTrue_AndSessionGone()
    {
        var sm = new SessionManager();
        var id = sm.Open(NewWindow());

        Assert.That(sm.Remove(id), Is.True);
        Assert.That(sm.TryGet(id, out _), Is.False);
    }

    [Test]
    public void Remove_UnknownId_ReturnsFalse()
    {
        var sm = new SessionManager();
        Assert.That(sm.Remove("nope"), Is.False);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Flax.Mcp.Tests/Flax.Mcp.Tests.csproj`
Expected: FAIL — `SessionManager` does not exist (compile error).

- [ ] **Step 3: Implement SessionManager**

Create `Flax.Mcp/SessionManager.cs`:

```csharp
using System.Collections.Concurrent;
using Flax.Windows;

namespace Flax.Mcp;

/// <summary>
/// Keeps FlaxWindow sessions alive across stateless MCP tool calls, keyed by a short sessionId.
/// The server process is long-lived (stdio), so this is registered as a singleton.
/// </summary>
public sealed class SessionManager
{
    private readonly ConcurrentDictionary<string, FlaxWindow> _sessions = new();
    private int _counter;

    public string Open(FlaxWindow window)
    {
        var id = "s" + Interlocked.Increment(ref _counter);
        _sessions[id] = window;
        return id;
    }

    public bool TryGet(string sessionId, out FlaxWindow window)
        => _sessions.TryGetValue(sessionId ?? string.Empty, out window!);

    public bool Remove(string sessionId)
    {
        if (_sessions.TryRemove(sessionId ?? string.Empty, out var window))
        {
            window.Dispose();
            return true;
        }
        return false;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Flax.Mcp.Tests/Flax.Mcp.Tests.csproj`
Expected: PASS (5 tests).

- [ ] **Step 5: Register SessionManager in Program.cs**

In `Flax.Mcp/Program.cs`, add the SessionManager singleton registration right after the `WindowsAutomation` line:

```csharp
builder.Services.AddSingleton<WindowsAutomation>();
builder.Services.AddSingleton<SessionManager>();
```

`SessionManager` is in the `Flax.Mcp` namespace, which is the same as the top-level `Program` — no extra `using` is needed.

- [ ] **Step 6: Build to confirm the server compiles**

Run: `dotnet build Flax.Mcp/Flax.Mcp.csproj`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add Flax.Mcp/SessionManager.cs Flax.Mcp/Program.cs Flax.Mcp.Tests/SessionManagerTests.cs
git commit -m "feat: add SessionManager with tests and register it in the server"
```

---

## Task 4: JSON helper and WindowTools

**Files:**
- Create: `Flax.Mcp/Json.cs`
- Create: `Flax.Mcp/Tools/WindowTools.cs`

- [ ] **Step 1: Create the JSON helper**

Create `Flax.Mcp/Json.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Flax.Mcp;

/// <summary>Serializes tool responses to compact JSON, omitting null fields to save tokens.</summary>
internal static class Json
{
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Of(object value) => JsonSerializer.Serialize(value, Options);
}
```

- [ ] **Step 2: Create WindowTools**

Create `Flax.Mcp/Tools/WindowTools.cs`:

```csharp
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
```

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build Flax.Mcp/Flax.Mcp.csproj`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add Flax.Mcp/Json.cs Flax.Mcp/Tools/WindowTools.cs
git commit -m "feat: add JSON helper and window tools"
```

---

## Task 5: InspectionTools (get_element_tree / find_element / capture_window)

**Files:**
- Create: `Flax.Mcp/Tools/InspectionTools.cs`

- [ ] **Step 1: Create InspectionTools**

Create `Flax.Mcp/Tools/InspectionTools.cs`:

```csharp
using System.Collections.Generic;
using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Flax.Mcp.Tools;

[McpServerToolType]
public static class InspectionTools
{
    [McpServerTool, Description("Return the window's UIA element tree as token-efficient JSON. Each node has a sequential 'id' valid only in this snapshot; pass it to click(elementId). Re-call each turn.")]
    public static string GetElementTree(SessionManager sessions, string sessionId, int maxDepth = -1, bool includeOffscreen = false)
    {
        if (!sessions.TryGet(sessionId, out var window))
            return Json.Of(new { ok = false, error = "session_not_found", hint = "Call open_window first." });

        var json = window.GetElementTreeAsJson(maxDepth, includeOffscreen);
        return json ?? Json.Of(new { ok = false, error = "tree_unavailable", hint = "Root not accessible (e.g. WinUI3). Use capture_window + Vision instead." });
    }

    [McpServerTool, Description("Find a single element by its accessible name and register it in the snapshot. Returns its id, rect [x,y,w,h] and center [x,y]. Use the id with click, or the center coordinates as a fallback.")]
    public static string FindElement(SessionManager sessions, string sessionId, string name)
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
    }

    [McpServerTool, Description("Capture a screenshot of the session's window as PNG (for Vision when the UIA tree is too shallow). Image pixel (0,0) maps to screen coordinate windowOrigin [x,y]; add windowOrigin to the pixel you pick before calling click(x,y).")]
    public static IEnumerable<ContentBlock> CaptureWindow(SessionManager sessions, string sessionId)
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
}
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build Flax.Mcp/Flax.Mcp.csproj`
Expected: PASS.

If `ContentBlock` / `TextContentBlock` / `ImageContentBlock` do not resolve from `ModelContextProtocol.Protocol`, find their namespace in the restored SDK (search the package for `class ImageContentBlock`) and correct the `using`. The `ImageContentBlock.FromBytes(bytes, "image/png")` API and these type names are confirmed from the SDK docs.

- [ ] **Step 3: Commit**

```bash
git add Flax.Mcp/Tools/InspectionTools.cs
git commit -m "feat: add inspection tools (element tree, find, capture)"
```

---

## Task 6: ClickService, SendKeysMap (TDD) and ActionTools

**Files:**
- Create: `Flax.Mcp/ClickService.cs`
- Create: `Flax.Mcp/SendKeysMap.cs`
- Create: `Flax.Mcp/Tools/ActionTools.cs`
- Test: `Flax.Mcp.Tests/ClickServiceTests.cs`
- Test: `Flax.Mcp.Tests/SendKeysMapTests.cs`

- [ ] **Step 1: Write the failing ClickService tests**

Create `Flax.Mcp.Tests/ClickServiceTests.cs`:

```csharp
using System.Collections.Generic;
using Flax.Mcp;
using NUnit.Framework;

namespace Flax.Mcp.Tests;

public class ClickServiceTests
{
    private sealed class FakeClickable : IClickable
    {
        public (int X, int Y) Center { get; init; } = (100, 200);
        public bool UiaSucceeds { get; init; } = true;
        public bool UiaCalled { get; private set; }

        public bool TryUiaClick(ClickKind kind, out string? error)
        {
            UiaCalled = true;
            error = UiaSucceeds ? null : "boom";
            return UiaSucceeds;
        }
    }

    private sealed class FakeLookup : IElementLookup
    {
        private readonly IClickable? _result;
        public FakeLookup(IClickable? result) => _result = result;
        public IClickable? FindById(int id) => _result;
    }

    [Test]
    public void Click_ById_UiaSucceeds_ReturnsUia_NoCoordClick()
    {
        var coordCalls = new List<(int, int, ClickKind)>();
        var outcome = new ClickService().Click(
            new FakeLookup(new FakeClickable { UiaSucceeds = true }),
            elementId: 3, x: null, y: null, ClickKind.Left,
            (x, y, k) => coordCalls.Add((x, y, k)));

        Assert.That(outcome.Success, Is.True);
        Assert.That(outcome.Method, Is.EqualTo("uia"));
        Assert.That(coordCalls, Is.Empty);
    }

    [Test]
    public void Click_ById_UiaFails_FallsBackToElementCenter()
    {
        var coordCalls = new List<(int, int, ClickKind)>();
        var outcome = new ClickService().Click(
            new FakeLookup(new FakeClickable { UiaSucceeds = false, Center = (40, 60) }),
            elementId: 3, x: null, y: null, ClickKind.Left,
            (x, y, k) => coordCalls.Add((x, y, k)));

        Assert.That(outcome.Success, Is.True);
        Assert.That(outcome.Method, Is.EqualTo("coord-fallback"));
        Assert.That(coordCalls, Has.Count.EqualTo(1));
        Assert.That(coordCalls[0], Is.EqualTo((40, 60, ClickKind.Left)));
    }

    [Test]
    public void Click_ById_NotFound_ReturnsElementNotFound()
    {
        var outcome = new ClickService().Click(
            new FakeLookup(null),
            elementId: 9, x: null, y: null, ClickKind.Left,
            (_, _, _) => { });

        Assert.That(outcome.Success, Is.False);
        Assert.That(outcome.Error, Is.EqualTo("element_not_found"));
    }

    [Test]
    public void Click_ByCoordinates_ClicksDirectly()
    {
        var coordCalls = new List<(int, int, ClickKind)>();
        var outcome = new ClickService().Click(
            new FakeLookup(null),
            elementId: null, x: 11, y: 22, ClickKind.Right,
            (x, y, k) => coordCalls.Add((x, y, k)));

        Assert.That(outcome.Success, Is.True);
        Assert.That(outcome.Method, Is.EqualTo("coord"));
        Assert.That(coordCalls[0], Is.EqualTo((11, 22, ClickKind.Right)));
    }

    [Test]
    public void Click_NoTarget_ReturnsBadRequest()
    {
        var outcome = new ClickService().Click(
            new FakeLookup(null),
            elementId: null, x: null, y: null, ClickKind.Left,
            (_, _, _) => { });

        Assert.That(outcome.Success, Is.False);
        Assert.That(outcome.Error, Is.EqualTo("bad_request"));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Flax.Mcp.Tests/Flax.Mcp.Tests.csproj`
Expected: FAIL — `ClickService`, `IClickable`, `IElementLookup`, `ClickKind` do not exist.

- [ ] **Step 3: Implement ClickService and adapters**

Create `Flax.Mcp/ClickService.cs`:

```csharp
using System;
using Flax.Windows;

namespace Flax.Mcp;

public enum ClickKind { Left, LeftDouble, Right }

public interface IClickable
{
    (int X, int Y) Center { get; }
    bool TryUiaClick(ClickKind kind, out string? error);
}

public interface IElementLookup
{
    IClickable? FindById(int id);
}

public sealed record ClickOutcome(bool Success, string Method, string? Error = null);

/// <summary>
/// Decides how to click: ID-priority (UIA Invoke) with a coordinate fallback on the element's
/// center when UIA fails, or a direct coordinate click when only x,y are given.
/// </summary>
public sealed class ClickService
{
    public ClickOutcome Click(
        IElementLookup lookup,
        int? elementId,
        int? x,
        int? y,
        ClickKind kind,
        Action<int, int, ClickKind> coordClick)
    {
        if (elementId.HasValue)
        {
            var element = lookup.FindById(elementId.Value);
            if (element == null)
                return new ClickOutcome(false, "none", "element_not_found");

            if (element.TryUiaClick(kind, out _))
                return new ClickOutcome(true, "uia");

            coordClick(element.Center.X, element.Center.Y, kind);
            return new ClickOutcome(true, "coord-fallback");
        }

        if (x.HasValue && y.HasValue)
        {
            coordClick(x.Value, y.Value, kind);
            return new ClickOutcome(true, "coord");
        }

        return new ClickOutcome(false, "none", "bad_request");
    }
}

/// <summary>Adapts a Flax UIElement to IClickable.</summary>
public sealed class UiElementClickable : IClickable
{
    private readonly UIElement _element;
    public UiElementClickable(UIElement element) => _element = element;

    public (int X, int Y) Center => (_element.CenterX, _element.CenterY);

    public bool TryUiaClick(ClickKind kind, out string? error)
    {
        try
        {
            switch (kind)
            {
                case ClickKind.LeftDouble: _element.DoubleClick(); break;
                case ClickKind.Right: _element.RightClick(); break;
                default: _element.Click(); break;
            }
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}

/// <summary>Looks up elements in a FlaxWindow's current snapshot.</summary>
public sealed class FlaxWindowElementLookup : IElementLookup
{
    private readonly FlaxWindow _window;
    public FlaxWindowElementLookup(FlaxWindow window) => _window = window;

    public IClickable? FindById(int id)
    {
        var element = _window.GetElementById(id);
        return element == null ? null : new UiElementClickable(element);
    }
}
```

- [ ] **Step 4: Run ClickService tests to verify they pass**

Run: `dotnet test Flax.Mcp.Tests/Flax.Mcp.Tests.csproj --filter ClickServiceTests`
Expected: PASS (5 tests).

- [ ] **Step 5: Write the failing SendKeysMap tests**

Create `Flax.Mcp.Tests/SendKeysMapTests.cs`:

```csharp
using Flax.Mcp;
using Flax.Windows;
using NUnit.Framework;

namespace Flax.Mcp.Tests;

public class SendKeysMapTests
{
    [Test]
    public void TryGet_KnownKey_ReturnsTrue()
        => Assert.That(SendKeysMap.TryGet("ENTER", out _), Is.True);

    [Test]
    public void TryGet_IsCaseInsensitive_AndTrims()
        => Assert.That(SendKeysMap.TryGet("  enter ", out _), Is.True);

    [Test]
    public void TryGet_Combo_ReturnsTrue()
        => Assert.That(SendKeysMap.TryGet("CTRL+A", out _), Is.True);

    [Test]
    public void TryGet_UnknownKey_ReturnsFalse()
        => Assert.That(SendKeysMap.TryGet("F13", out _), Is.False);
}
```

- [ ] **Step 6: Run tests to verify they fail**

Run: `dotnet test Flax.Mcp.Tests/Flax.Mcp.Tests.csproj --filter SendKeysMapTests`
Expected: FAIL — `SendKeysMap` does not exist.

- [ ] **Step 7: Implement SendKeysMap**

Create `Flax.Mcp/SendKeysMap.cs`:

```csharp
using System;
using System.Collections.Generic;
using Flax.Windows;

namespace Flax.Mcp;

/// <summary>Maps a special-key name to an action on FlaxKeyboard. Use type_text for arbitrary text.</summary>
public static class SendKeysMap
{
    private static readonly Dictionary<string, Action<FlaxKeyboard>> Map =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["ENTER"] = k => k.Enter(),
            ["ESC"] = k => k.Esc(),
            ["TAB"] = k => k.Tab(),
            ["SPACE"] = k => k.Space(),
            ["BACKSPACE"] = k => k.BackSpace(),
            ["DELETE"] = k => k.Delete(),
            ["UP"] = k => k.Up(),
            ["DOWN"] = k => k.Down(),
            ["LEFT"] = k => k.Left(),
            ["RIGHT"] = k => k.Right(),
            ["CTRL+A"] = k => k.CtrlA(),
            ["CTRL+C"] = k => k.CtrlC(),
            ["CTRL+V"] = k => k.CtrlV(),
        };

    public static bool TryGet(string keys, out Action<FlaxKeyboard> action)
        => Map.TryGetValue((keys ?? string.Empty).Trim(), out action!);
}
```

- [ ] **Step 8: Run SendKeysMap tests to verify they pass**

Run: `dotnet test Flax.Mcp.Tests/Flax.Mcp.Tests.csproj --filter SendKeysMapTests`
Expected: PASS (4 tests).

- [ ] **Step 9: Implement ActionTools**

Create `Flax.Mcp/Tools/ActionTools.cs`:

```csharp
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
        bool doubleClick = false)
    {
        if (!sessions.TryGet(sessionId, out var window))
            return Json.Of(new { ok = false, error = "session_not_found", hint = "Call open_window first." });

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
            return Json.Of(new { ok = true, method = outcome.Method });

        return Json.Of(new
        {
            ok = false,
            error = outcome.Error,
            hint = outcome.Error == "element_not_found" ? "Re-run get_element_tree for a fresh snapshot." : null
        });
    }

    [McpServerTool, Description("Type literal text into the session's focused control.")]
    public static string TypeText(SessionManager sessions, string sessionId, string text)
    {
        if (!sessions.TryGet(sessionId, out var window))
            return Json.Of(new { ok = false, error = "session_not_found", hint = "Call open_window first." });
        window.Activate();
        new FlaxKeyboard().Type(text);
        return Json.Of(new { ok = true });
    }

    [McpServerTool, Description("Press a special key or combo. Supported: ENTER, ESC, TAB, SPACE, BACKSPACE, DELETE, UP, DOWN, LEFT, RIGHT, CTRL+A, CTRL+C, CTRL+V.")]
    public static string SendKeys(SessionManager sessions, string sessionId, string keys)
    {
        if (!sessions.TryGet(sessionId, out var window))
            return Json.Of(new { ok = false, error = "session_not_found", hint = "Call open_window first." });
        if (!SendKeysMap.TryGet(keys, out var action))
            return Json.Of(new { ok = false, error = "unknown_key", hint = "Use type_text for arbitrary text." });
        window.Activate();
        action(new FlaxKeyboard());
        return Json.Of(new { ok = true });
    }

    [McpServerTool, Description("Scroll the session's window. Positive lines scroll up/right; negative down/left. Set horizontal=true for horizontal scroll.")]
    public static string Scroll(SessionManager sessions, string sessionId, double lines, bool horizontal = false)
    {
        if (!sessions.TryGet(sessionId, out var window))
            return Json.Of(new { ok = false, error = "session_not_found", hint = "Call open_window first." });
        window.Activate();
        if (horizontal) FlaxMouse.HorizontalScroll(lines);
        else FlaxMouse.VerticalScroll(lines);
        return Json.Of(new { ok = true });
    }
}
```

- [ ] **Step 10: Run the full test suite**

Run: `dotnet test Flax.Mcp.Tests/Flax.Mcp.Tests.csproj`
Expected: PASS (14 tests: 5 SessionManager + 5 ClickService + 4 SendKeysMap).

- [ ] **Step 11: Commit**

```bash
git add Flax.Mcp/ClickService.cs Flax.Mcp/SendKeysMap.cs Flax.Mcp/Tools/ActionTools.cs Flax.Mcp.Tests/ClickServiceTests.cs Flax.Mcp.Tests/SendKeysMapTests.cs
git commit -m "feat: add click service, send-keys map, and action tools with tests"
```

---

## Task 7: Integration smoke test (mspaint round trip)

**Files:**
- Create: `Flax.Mcp.Tests/ToolSmokeTests.cs`

This test calls the tool methods directly (no transport) against a real classic app. It is marked `[Explicit]` so it does not run in CI — it must be run interactively on a Windows desktop.

- [ ] **Step 1: Write the smoke test**

Create `Flax.Mcp.Tests/ToolSmokeTests.cs`:

```csharp
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
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build Flax.Mcp.Tests/Flax.Mcp.Tests.csproj`
Expected: PASS.

- [ ] **Step 3: Run the smoke test interactively**

Run: `dotnet test Flax.Mcp.Tests/Flax.Mcp.Tests.csproj --filter "FullyQualifiedName~ToolSmokeTests"`
Expected: PASS — mspaint launches, the tree contains `controlType`, an image block is returned, and the window closes. (Selecting the `[Explicit]` fixture by filter runs it; if your runner still skips it, run the test from the IDE Test Explorer instead. Do not leave mspaint open — the test closes it.)

- [ ] **Step 4: Commit**

```bash
git add Flax.Mcp.Tests/ToolSmokeTests.cs
git commit -m "test: add explicit integration smoke for MCP tools against mspaint"
```

---

## Task 8: Claude Desktop registration, docs, and manual E2E

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Publish the server**

Run: `dotnet publish Flax.Mcp/Flax.Mcp.csproj -c Release`
Expected: PASS. Note the output path of `Flax.Mcp.exe` (e.g. `Flax.Mcp/bin/Release/net8.0-windows/publish/Flax.Mcp.exe`).

- [ ] **Step 2: Register the server in Claude Desktop**

Add to `%APPDATA%\Claude\claude_desktop_config.json` (create the file if missing), replacing the path with the published exe path from Step 1:

```json
{
  "mcpServers": {
    "flax": {
      "command": "C:\\Users\\mtmar\\source\\repos\\Flax\\Flax.Mcp\\bin\\Release\\net8.0-windows\\publish\\Flax.Mcp.exe"
    }
  }
}
```

Restart Claude Desktop. Expected: the `flax` server appears with its tools (ping, launch_app, list_windows, open_window, activate_window, close_window, get_element_tree, find_element, capture_window, click, type_text, send_keys, scroll).

- [ ] **Step 3: Manual E2E — classic app (UIA path)**

In Claude Desktop, prompt: "Open Notepad and type 'hello' using the flax tools." Verify the model calls launch_app → open_window → (get_element_tree) → type_text and that "hello" appears.

- [ ] **Step 4: Manual E2E — calculator (Vision path)**

In Claude Desktop, prompt: "電卓で1+1を計算して (use the flax tools)." Verify:
1. launch_app("calc.exe") → open_window("%電卓%" or "%Calculator%").
2. get_element_tree returns a shallow tree (WinUI3) with no digit buttons.
3. The model calls capture_window, reads the "1", "+", "=" pixel positions, adds windowOrigin, and calls click(x,y) for each.
4. capture_window again confirms the result shows 2.

- [ ] **Step 5: Document the MCP server in the README**

Add a section to `README.md` after the "Getting the UI element tree (for LLMs)" section:

````markdown
### MCP server (drive apps from an LLM)

`Flax.Mcp` is an MCP server (stdio) that exposes Flax to MCP clients such as Claude Desktop / Claude Code. It lets an LLM launch an app, read its UI element tree, and click — with a screenshot+Vision fallback for WinUI3 apps whose UIA tree is shallow.

**Tools:** `launch_app`, `list_windows`, `open_window`, `activate_window`, `close_window`, `get_element_tree`, `find_element`, `capture_window`, `click`, `type_text`, `send_keys`, `scroll`.

**Workflow:** `open_window` returns a `sessionId` that every other tool takes. Element `id`s come from `get_element_tree`/`find_element` and are valid only within the latest snapshot. `click` is ID-priority (UIA) with a coordinate fallback; for WinUI3 apps, call `capture_window`, read pixel coordinates, add the returned `windowOrigin`, and call `click(x, y)`.

**Register in Claude Desktop** (`%APPDATA%\Claude\claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "flax": { "command": "<path>\\Flax.Mcp.exe" }
  }
}
```
````

- [ ] **Step 6: Commit**

```bash
git add README.md
git commit -m "docs: document the Flax.Mcp server and Claude Desktop registration"
```

---

## Notes / Known Risks

- **Multi-targeting (Task 1)** is the highest-risk step: `net8.0-windows` must resolve FlaUI 3.0, System.Drawing.Common, WinForms, and WPF. FlaUI 3.0 supports modern .NET, so this is expected to work, but the build gate in Task 1 Step 2 is where any issue surfaces.
- **MCP SDK API names** (`AddMcpServer`, `WithStdioServerTransport`, `WithToolsFromAssembly`, `[McpServerTool]`, `ImageContentBlock.FromBytes`) are confirmed from the official C# SDK docs. If a namespace differs in the installed preview version, correct the `using` (the build error names the missing symbol).
- **Screenshot coordinate space:** `capture_window` returns `windowOrigin` and the click tool uses absolute screen coordinates; the LLM must add the origin to image pixels. This is documented in the tool description and README.
- **Session lifetime:** v1 has no idle timeout; sessions live until `close_window` or server exit. Add a timeout later if leaked sessions become a problem.
