using System.Text.Json;

namespace Flax.Mcp.Llm;

public sealed record LocateResult(
    bool Found,
    int? Id = null,
    int? Px = null,
    int? Py = null,
    double? Confidence = null,
    string? Reasoning = null);

/// <summary>
/// Parses the locator model's JSON reply. Tolerant: extracts the first {...} block, and on any parse
/// failure (or missing required field) returns Found=false with the raw text in Reasoning so the
/// caller can fall back gracefully instead of erroring.
/// </summary>
public static class LocateResultParser
{
    public static LocateResult ParseTree(string text) => Parse(text, vision: false);
    public static LocateResult ParseVision(string text) => Parse(text, vision: true);

    private static LocateResult Parse(string text, bool vision)
    {
        var json = ExtractJsonObject(text);
        if (json == null) return new LocateResult(false, Reasoning: Trim(text));

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var found = root.TryGetProperty("found", out var f) && f.ValueKind == JsonValueKind.True;
            double? confidence = root.TryGetProperty("confidence", out var c) && c.TryGetDouble(out var cv) ? cv : null;
            string? reasoning = root.TryGetProperty("reasoning", out var r) && r.ValueKind == JsonValueKind.String ? r.GetString() : null;

            if (!found)
                return new LocateResult(false, Confidence: confidence, Reasoning: reasoning ?? Trim(text));

            if (vision)
            {
                int? px = root.TryGetProperty("px", out var pxe) && pxe.TryGetInt32(out var pxv) ? pxv : null;
                int? py = root.TryGetProperty("py", out var pye) && pye.TryGetInt32(out var pyv) ? pyv : null;
                if (px == null || py == null)
                    return new LocateResult(false, Confidence: confidence, Reasoning: reasoning ?? Trim(text));
                return new LocateResult(true, Px: px, Py: py, Confidence: confidence, Reasoning: reasoning);
            }
            else
            {
                int? id = root.TryGetProperty("id", out var ide) && ide.TryGetInt32(out var idv) ? idv : null;
                if (id == null)
                    return new LocateResult(false, Confidence: confidence, Reasoning: reasoning ?? Trim(text));
                return new LocateResult(true, Id: id, Confidence: confidence, Reasoning: reasoning);
            }
        }
        catch (JsonException)
        {
            return new LocateResult(false, Reasoning: Trim(text));
        }
    }

    private static string? ExtractJsonObject(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text.Substring(start, end - start + 1) : null;
    }

    private static string Trim(string text) => text.Length <= 500 ? text : text.Substring(0, 500);
}
