using System.Threading;
using System.Threading.Tasks;

namespace Flax.Mcp.Llm;

public enum LocateMode { Auto, Tree, Vision }

/// <summary>The cheap server-side locator model, abstracted for testing.</summary>
public interface IElementLocator
{
    LocatorStatus Status { get; }
    Task<LocateResult> LocateInTreeAsync(string treeJson, string target, CancellationToken ct);
    Task<LocateResult> LocateByVisionAsync(byte[] png, string target, CancellationToken ct);
}

/// <summary>The window surface LocateService needs; adapts FlaxWindow for testability.</summary>
public interface ILocateWindow
{
    string? GetTreeJson();
    byte[]? CapturePng();
    int Left { get; }
    int Top { get; }
}

public sealed record LocateOutcome(
    bool Ok,
    string? Mode = null,          // "tree" | "vision"
    int? ElementId = null,
    int? X = null,
    int? Y = null,
    double? Confidence = null,
    string? Reasoning = null,
    string? Error = null,
    string? Hint = null);

public static class LocateModeParser
{
    public static LocateMode Parse(string? mode) => (mode ?? "").Trim().ToLowerInvariant() switch
    {
        "tree" => LocateMode.Tree,
        "vision" => LocateMode.Vision,
        _ => LocateMode.Auto
    };
}
