using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace Flax.Mcp.Llm;

/// <summary>
/// IElementLocator backed by a Microsoft.Extensions.AI IChatClient — the cheap, server-side model
/// configured independently of the MCP client's model.
/// </summary>
public sealed class ElementLocator : IElementLocator
{
    private readonly IChatClient? _client;
    private readonly int _maxTokens;
    private readonly string _model;

    public LocatorStatus Status { get; }

    public ElementLocator(IChatClient? client, LocatorStatus status, int maxOutputTokens, string model)
    {
        _client = client;
        Status = status;
        _maxTokens = maxOutputTokens;
        _model = model;
    }

    private const string TreeSystem =
        "You locate exactly one UI element in a Windows UIA element tree given as JSON. Each node has a " +
        "numeric \"id\". Reply with ONLY compact JSON: {\"found\":bool,\"id\":number,\"confidence\":0..1,\"reasoning\":\"short\"}. " +
        "If nothing matches, reply {\"found\":false}.";

    private const string VisionSystem =
        "You locate exactly one UI element in a screenshot. Reply with ONLY compact JSON: " +
        "{\"found\":bool,\"px\":number,\"py\":number,\"confidence\":0..1,\"reasoning\":\"short\"} where px,py are the " +
        "pixel coordinates of the element's center in the image (origin top-left). If not visible, reply {\"found\":false}.";

    public async Task<LocateResult> LocateInTreeAsync(string treeJson, string target, CancellationToken ct)
    {
        var messages = new[]
        {
            new ChatMessage(ChatRole.System, TreeSystem),
            new ChatMessage(ChatRole.User, $"Target: {target}\n\nUIA tree:\n{treeJson}")
        };
        var resp = await _client!.GetResponseAsync(messages, Options(), ct);
        return LocateResultParser.ParseTree(resp.Text ?? "");
    }

    public async Task<LocateResult> LocateByVisionAsync(byte[] png, string target, CancellationToken ct)
    {
        var user = new ChatMessage(ChatRole.User, new AIContent[]
        {
            new TextContent($"Target: {target}\nReturn the pixel coordinates of its center."),
            new DataContent(png, "image/png")
        });
        var messages = new[] { new ChatMessage(ChatRole.System, VisionSystem), user };
        var resp = await _client!.GetResponseAsync(messages, Options(), ct);
        return LocateResultParser.ParseVision(resp.Text ?? "");
    }

    private ChatOptions Options() => new()
    {
        MaxOutputTokens = _maxTokens,
        ModelId = string.IsNullOrEmpty(_model) ? null : _model
    };
}
