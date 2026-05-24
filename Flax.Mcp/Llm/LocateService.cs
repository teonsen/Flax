using System;
using System.Threading;
using System.Threading.Tasks;

namespace Flax.Mcp.Llm;

/// <summary>
/// Orchestrates element location: chooses tree vs vision, performs the auto fallback (tree first,
/// then vision when the tree is unavailable or yields nothing), and converts vision pixel
/// coordinates to absolute screen coordinates. FlaUI-free via ILocateWindow / IElementLocator.
/// </summary>
public sealed class LocateService
{
    public async Task<LocateOutcome> LocateAsync(
        IElementLocator locator, ILocateWindow window, string target, LocateMode mode, CancellationToken ct)
    {
        switch (locator.Status)
        {
            case LocatorStatus.NotConfigured:
                return new LocateOutcome(false, Error: "llm_not_configured",
                    Hint: "Set FLAX_LLM_PROVIDER/FLAX_LLM_MODEL and the provider's API key env var, or locate on the client side.");
            case LocatorStatus.KeyMissing:
                return new LocateOutcome(false, Error: "llm_key_missing",
                    Hint: "Provider is configured but its API key environment variable is empty.");
        }

        try
        {
            if (mode == LocateMode.Vision)
                return await VisionAsync(locator, window, target, ct);

            // Tree or Auto: try the UIA tree first.
            var treeJson = window.GetTreeJson();
            if (treeJson != null)
            {
                var tree = await locator.LocateInTreeAsync(treeJson, target, ct);
                if (tree.Found && tree.Id.HasValue)
                    return new LocateOutcome(true, "tree", ElementId: tree.Id,
                        Confidence: tree.Confidence, Reasoning: tree.Reasoning);

                if (mode == LocateMode.Tree)
                    return new LocateOutcome(false, Error: "element_not_found",
                        Hint: "No matching element in the UIA tree.", Reasoning: tree.Reasoning);
            }
            else if (mode == LocateMode.Tree)
            {
                return new LocateOutcome(false, Error: "element_not_found",
                    Hint: "UIA tree unavailable (e.g. WinUI3). Try mode=vision or auto.");
            }

            // Auto fallback to vision.
            return await VisionAsync(locator, window, target, ct);
        }
        catch (Exception ex)
        {
            return new LocateOutcome(false, Error: "llm_error", Hint: ex.Message);
        }
    }

    private static async Task<LocateOutcome> VisionAsync(
        IElementLocator locator, ILocateWindow window, string target, CancellationToken ct)
    {
        var png = window.CapturePng();
        if (png == null || png.Length == 0)
            return new LocateOutcome(false, Error: "element_not_found", Hint: "Window capture failed.");

        var v = await locator.LocateByVisionAsync(png, target, ct);
        if (v.Found && v.Px.HasValue && v.Py.HasValue)
            return new LocateOutcome(true, "vision",
                X: window.Left + v.Px.Value, Y: window.Top + v.Py.Value,
                Confidence: v.Confidence, Reasoning: v.Reasoning);

        return new LocateOutcome(false, Error: "element_not_found",
            Hint: "Could not locate the target by vision.", Reasoning: v.Reasoning);
    }
}
