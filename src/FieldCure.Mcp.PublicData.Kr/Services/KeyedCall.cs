using System.Text.Json;
using ModelContextProtocol.Server;

namespace FieldCure.Mcp.PublicData.Kr.Services;

/// <summary>
/// Helper that wires <see cref="ApiKeyResolver"/> into a tool invocation: resolves
/// the key, runs the provided HTTP operation, and on <see cref="InvalidApiKeyException"/>
/// invalidates the cache and retries once. If no key can be resolved (soft-fail) or the
/// retry also fails, a JSON error response is produced with the env var hint per ADR-001.
/// </summary>
public static class KeyedCall
{
    /// <summary>
    /// Runs <paramref name="operation"/> with a resolved API key and handles the
    /// resolve→try→invalidate→retry loop. At most one retry per invocation; further
    /// re-elicitation is gated by <see cref="ApiKeyResolver"/>'s session-level cap.
    /// </summary>
    public static async Task<string> RunAsync(
        McpServer server,
        ApiKeyResolver resolver,
        Func<string, Task<string>> operation,
        CancellationToken ct)
    {
        var apiKey = await resolver.ResolveAsync(server, ct);
        if (apiKey is null)
            return JsonSerializer.Serialize(new { error = resolver.BuildSoftFailMessage() }, McpJson.Options);

        try
        {
            return await operation(apiKey);
        }
        catch (InvalidApiKeyException)
        {
            resolver.Invalidate();
        }

        // Retry path: resolver will skip static sources and attempt a re-elicit
        // (subject to MaxElicitAttempts).
        var retryKey = await resolver.ResolveAsync(server, ct);
        if (retryKey is null)
            return JsonSerializer.Serialize(new { error = resolver.BuildSoftFailMessage() }, McpJson.Options);

        try
        {
            return await operation(retryKey);
        }
        catch (InvalidApiKeyException ex)
        {
            resolver.Invalidate();
            return JsonSerializer.Serialize(
                new { error = $"API key rejected after retry: {ex.Message}. {resolver.BuildSoftFailMessage()}" },
                McpJson.Options);
        }
    }
}
