# Flax (This project is in preparation)
FlaUI wrapper(UIA3 only), adding Java and OpenCV feature.

### Why the wrapper for FlaUI now?
Because it would be nice to code Windows and Mac in the same framework.
The "x" in Flax meant to be x(cross) platform.

```csharp
using Flax;

    var f = new WindowsAutomation();
    f.Process.Run("calc.exe");

    using (var w = f.GetWindow("Calculator"))
    {
        w.GetElementByName("1")?.Click();
    }
```

### What is the OpenCV feature?
Only CV.Click() is available now.
You will need [Flax.CV.exe](https://github.com/teonsen/Flax.CV/releases) in your app's startup path.

```csharp
using Flax;

    var f = new WindowsAutomation();
    // Click the center point of matched area, if your template image matched in your screen.
    f.CV.Click($YourTemplateImagePath.bmp);
```

### Getting the UI element tree (for LLMs)
Get the whole UI tree of a window as JSON, hand it to an LLM, then act on the element the LLM picked by its `id`.

```csharp
using Flax;

    var f = new WindowsAutomation();
    f.Process.Run("mspaint.exe");

    using (var w = f.GetWindow("%Paint%"))   // "%...%" matches by Contains
    {
        // Token-efficient JSON tree. Offscreen elements are skipped by default.
        string json = w.GetElementTreeAsJson();
        // ... let an LLM choose an element id from the json ...

        // Act on the chosen element by its id.
        w.GetElementById(7)?.Click();
    }
```

`GetElementTreeAsJson(int maxDepth = -1, bool includeOffscreen = false)` walks the window's descendants, assigns a sequential `id` to each node, and returns a JSON tree (`id`, `controlType`, `name`, `automationId`, `className`, `rect` as `[x,y,width,height]`, `enabled`, `visible`, nested `children`; empty fields are omitted to save tokens). `GetElementById(id)` returns the element from the most recent tree call so you can `Click()` it. IDs are valid within one snapshot — call `GetElementTreeAsJson` again each turn.

> **Note:** Works fully on classic Win32 / WinForms / WPF apps. On Windows 11 modern apps (WinUI3 / UWP, e.g. the new Calculator, Notepad, or Paint's canvas) the accessible tree is shallow because their controls live in XAML islands that out-of-process UI Automation cannot traverse — this is a UIA limitation, not a Flax one.

### MCP server (drive apps from an LLM)

`Flax.Mcp` is an MCP server (stdio) that exposes Flax to MCP clients such as Claude Desktop / Claude Code. It lets an LLM launch an app, read its UI element tree, and click — with a screenshot + Vision fallback for WinUI3 apps whose UIA tree is too shallow to traverse.

**Tools:** `launch_app`, `list_windows`, `open_window`, `activate_window`, `close_window`, `get_element_tree`, `find_element`, `capture_window`, `click`, `type_text`, `send_keys`, `scroll`.

**Workflow:** `open_window` returns a `sessionId` that every other tool takes. Element `id`s come from `get_element_tree` (returned as `{ "ok": true, "tree": ... }`) or `find_element`, and are valid only within the latest snapshot — re-read the tree each turn. `click` is ID-priority (UIA `Invoke`) with an automatic coordinate fallback. For WinUI3 apps where the tree is too shallow to expose the controls, call `capture_window`, read the pixel coordinates of the target from the returned PNG, add the returned `windowOrigin` `[x,y]` to convert image pixels to absolute screen coordinates, then call `click` with those `x,y`.

**Build and register in Claude Desktop:**

1. Publish the server:

   ```
   dotnet publish Flax.Mcp/Flax.Mcp.csproj -c Release
   ```

2. Add it to `%APPDATA%\Claude\claude_desktop_config.json` (create the file if it does not exist), using the published `Flax.Mcp.exe` path:

   ```json
   {
     "mcpServers": {
       "flax": { "command": "C:\\path\\to\\Flax.Mcp\\bin\\Release\\net8.0-windows\\publish\\Flax.Mcp.exe" }
     }
   }
   ```

3. Restart Claude Desktop. The `flax` tools then appear and an LLM can drive Windows apps (e.g. "open Notepad and type hello", or for WinUI3 apps "電卓で1+1を計算して" via the screenshot + Vision path).
