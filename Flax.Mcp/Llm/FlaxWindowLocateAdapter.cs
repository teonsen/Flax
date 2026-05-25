using Flax.Windows;

namespace Flax.Mcp.Llm;

/// <summary>Adapts a live FlaxWindow to the ILocateWindow surface LocateService needs.</summary>
public sealed class FlaxWindowLocateAdapter : ILocateWindow
{
    private readonly FlaxWindow _window;
    public FlaxWindowLocateAdapter(FlaxWindow window) => _window = window;

    public string? GetTreeJson() => _window.GetElementTreeAsJson(-1, false);

    public byte[]? CapturePng()
    {
        _window.Activate();
        return _window.CaptureToPngBytes();
    }

    public int Left => _window.Left;
    public int Top => _window.Top;
}
