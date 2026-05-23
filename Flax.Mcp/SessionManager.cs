using System.Collections.Concurrent;
using Flax.Windows;

namespace Flax.Mcp;

/// <summary>
/// Keeps FlaxWindow sessions alive across stateless MCP tool calls, keyed by a short sessionId.
/// The server process is long-lived (stdio), so this is registered as a singleton.
/// </summary>
public sealed class SessionManager
{
    private readonly ConcurrentDictionary<string, FlaxWindow> _sessions = new();
    private int _counter;

    public string Open(FlaxWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        var id = "s" + Interlocked.Increment(ref _counter);
        _sessions[id] = window;
        return id;
    }

    public bool TryGet(string sessionId, out FlaxWindow window)
        => _sessions.TryGetValue(sessionId ?? string.Empty, out window!);

    public bool Remove(string sessionId)
    {
        if (_sessions.TryRemove(sessionId ?? string.Empty, out var window))
        {
            window.Dispose();
            return true;
        }
        return false;
    }
}
