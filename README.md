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

### Using Flax.Mcp from other MCP clients (opencode / Cline / generic stdio)

`Flax.Mcp` is a standard stdio MCP server and works with any MCP-compatible client. The server-side **element-locator model** (used by `locate_element`, optional) can be configured with a separate, cheaper model — independent of whatever model the client itself uses. API keys are always read from environment variables, never from config files.

**opencode (`opencode.json`)**

```json
{
  "mcp": {
    "flax": {
      "type": "local",
      "command": ["C:\\path\\to\\Flax.Mcp.exe"],
      "environment": {
        "FLAX_LLM_PROVIDER": "openai",
        "FLAX_LLM_MODEL": "gpt-4o-mini",
        "OPENAI_API_KEY": "{env:OPENAI_API_KEY}"
      }
    }
  }
}
```

- `FLAX_LLM_PROVIDER` — `openai` / `azure` / `anthropic`
- `FLAX_LLM_MODEL` — the **cheap model** name for element location (e.g. `gpt-4o-mini`)
- API key env-var: `OPENAI_API_KEY` / `AZURE_OPENAI_API_KEY` / `ANTHROPIC_API_KEY`
  (use `FLAX_LLM_API_KEY_ENV` to specify a different env-var name)
- Azure additionally requires `FLAX_LLM_BASE_URL` (the Azure OpenAI endpoint URL)

**Cline / generic stdio clients**

Register `Flax.Mcp.exe` as a stdio command and inject `FLAX_LLM_*` plus the provider's API key via the client's `env` block, following the same pattern as above.

**Separation of concerns**

- **The client's model** (reasoning, tool selection) is configured in the client as usual.
- **Flax.Mcp's `Llm` config** is only for `locate_element` — a cheap dedicated model. **All other tools work without it.** If `Llm` is not configured, `locate_element` returns `llm_not_configured`; every other tool is unaffected.
- `locate_element` offloads UI-tree / Vision inference to the cheap server-side model and returns only a small result (`elementId` or `x,y`) to the client, saving client-side tokens.

**`locate_element` tool**

- Input: `sessionId`, `target` (natural language, e.g. `"the 1 button"`), `mode?` (`auto` default / `tree` / `vision`)
- `auto` mode: tries the UIA tree first; if the tree is unavailable (e.g. WinUI3) or the target is not found, automatically falls back to Vision.
- Output: tree mode → `{ "ok": true, "mode": "tree", "elementId": <id> }`; vision mode → `{ "ok": true, "mode": "vision", "x": <n>, "y": <n> }` (absolute screen coordinates). Both formats can be passed directly to `click`.
- Error codes: `session_not_found`, `llm_not_configured`, `llm_key_missing`, `llm_error`, `element_not_found`.

**Optional fallback: `appsettings.json`**

If env vars are not set, the `"Llm"` section in `appsettings.json` (placed next to `Flax.Mcp.exe`) can supply `Provider`, `Model`, `BaseUrl`, and `ApiKeyEnvVar` — but **never put API keys in the file itself**; keys must always come from environment variables.
