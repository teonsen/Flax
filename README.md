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
