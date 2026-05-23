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

public sealed record ClickOutcome(bool Success, string? Method, string? Error = null, string? UiaError = null);

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
                return new ClickOutcome(false, null, "element_not_found");

            if (element.TryUiaClick(kind, out var uiaError))
                return new ClickOutcome(true, "uia");

            coordClick(element.Center.X, element.Center.Y, kind);
            return new ClickOutcome(true, "coord-fallback", UiaError: uiaError);
        }

        if (x.HasValue && y.HasValue)
        {
            coordClick(x.Value, y.Value, kind);
            return new ClickOutcome(true, "coord");
        }

        return new ClickOutcome(false, null, "bad_request");
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
