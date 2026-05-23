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
