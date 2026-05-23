using System.Text.Json;
using System.Text.Json.Serialization;

namespace Flax.Mcp;

/// <summary>Serializes tool responses to compact JSON, omitting null fields to save tokens.</summary>
internal static class Json
{
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Of(object value) => JsonSerializer.Serialize(value, Options);
}
