using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FieldCure.Mcp.PublicData.Kr;

/// <summary>
/// Shared JSON serialization options for MCP tool responses.
/// Uses relaxed encoding so non-ASCII characters (Korean, CJK, emoji, etc.)
/// are emitted as-is instead of \uXXXX escape sequences.
/// </summary>
internal static class McpJson
{
    /// <summary>Tool response options: snake_case, indented, skip nulls.</summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Simple indented options for normalized responses.</summary>
    public static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
