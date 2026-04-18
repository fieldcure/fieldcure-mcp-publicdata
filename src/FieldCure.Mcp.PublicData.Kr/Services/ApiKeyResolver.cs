using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace FieldCure.Mcp.PublicData.Kr.Services;

/// <summary>
/// Resolves the data.go.kr API key using the ADR-001 priority chain:
/// CLI arg (if provided at startup) → env var → MCP Elicitation → soft-fail.
/// Caches the resolved key in process memory for the session lifetime and
/// supports invalidation + re-elicitation on 401/403 with a per-session cap.
/// </summary>
public sealed class ApiKeyResolver
{
    /// <summary>
    /// Canonical env var name (ADR-001 Phase 1 rename from PUBLICDATA_API_KEY).
    /// </summary>
    public const string EnvVarName = "DATA_GO_KR_API_KEY";

    /// <summary>
    /// Legacy env var name retained for backward compatibility with existing users.
    /// Will be removed in a future major version.
    /// </summary>
    public const string LegacyEnvVarName = "PUBLICDATA_API_KEY";

    /// <summary>
    /// Maximum re-elicits per session (elicitation attempts after a cache invalidation).
    /// The initial elicit (when no cached/static-source key exists) is not counted.
    /// Per ADR-001 §5: "세션당 재시도 상한: 2회 (무한 루프 방지)".
    /// </summary>
    const int MaxReElicits = 2;

    readonly string? _cliKey;
    readonly SemaphoreSlim _gate = new(1, 1);

    string? _cachedKey;
    bool _hasElicitedBefore;
    int _reElicitCount;
    bool _staticSourcesExhausted;

    /// <summary>
    /// Creates a resolver. <paramref name="cliKey"/> is optional; if provided it takes
    /// precedence for the initial resolution but is not re-read after invalidation
    /// (CLI arg is inherently static per process).
    /// </summary>
    public ApiKeyResolver(string? cliKey = null)
    {
        _cliKey = string.IsNullOrWhiteSpace(cliKey) ? null : cliKey;
    }

    /// <summary>
    /// Gets whether the key has been invalidated via <see cref="Invalidate"/> at least once.
    /// Exposed for test/diagnostic purposes.
    /// </summary>
    internal bool StaticSourcesExhausted => _staticSourcesExhausted;

    /// <summary>
    /// Resolves an API key. Returns null on soft-fail (no static source, elicitation
    /// unsupported / declined / canceled / retry cap exhausted).
    /// </summary>
    /// <param name="server">The MCP server instance used to issue an elicit request when needed.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<string?> ResolveAsync(McpServer server, CancellationToken ct)
    {
        // Fast path without lock contention
        if (_cachedKey is not null) return _cachedKey;

        await _gate.WaitAsync(ct);
        try
        {
            if (_cachedKey is not null) return _cachedKey;

            // Static sources (only read on fresh resolution; skipped after invalidation
            // since a 401 proves the static-source key is bad).
            if (!_staticSourcesExhausted)
            {
                if (_cliKey is not null)
                {
                    _cachedKey = _cliKey;
                    return _cachedKey;
                }

                var envKey = Environment.GetEnvironmentVariable(EnvVarName)
                             ?? Environment.GetEnvironmentVariable(LegacyEnvVarName);
                if (!string.IsNullOrWhiteSpace(envKey))
                {
                    _cachedKey = envKey;
                    return _cachedKey;
                }
            }

            // Elicitation fallback (lazy).
            // Initial elicit is always allowed; subsequent elicits (after Invalidate)
            // are capped at MaxReElicits per ADR-001 §5.
            if (server.ClientCapabilities?.Elicitation is null) return null;
            if (_hasElicitedBefore)
            {
                if (_reElicitCount >= MaxReElicits) return null;
                _reElicitCount++;
            }

            _hasElicitedBefore = true;
            var elicited = await TryElicitAsync(server, ct);
            if (!string.IsNullOrWhiteSpace(elicited))
            {
                _cachedKey = elicited;
                return _cachedKey;
            }

            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Invalidates the cached key. Called by tools after a 401/403 response.
    /// Subsequent <see cref="ResolveAsync"/> calls skip static sources (CLI/env var)
    /// since those have been proven invalid, and proceed directly to elicitation
    /// (subject to the session retry cap).
    /// </summary>
    public void Invalidate()
    {
        _cachedKey = null;
        _staticSourcesExhausted = true;
    }

    /// <summary>
    /// Builds a soft-fail error message with the env var name hint and a note about
    /// Elicitation support for interactive clients.
    /// </summary>
    public string BuildSoftFailMessage() =>
        $"API key not configured. Set {EnvVarName} environment variable, " +
        "or use a client that supports MCP Elicitation.";

    /// <summary>
    /// Issues an MCP elicitation request with a single password-like string field.
    /// Returns the collected key or null if the user declined/canceled or the client errored.
    /// </summary>
    static async Task<string?> TryElicitAsync(McpServer server, CancellationToken ct)
    {
        var schema = new ElicitRequestParams.RequestSchema
        {
            Properties = new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>
            {
                ["api_key"] = new ElicitRequestParams.StringSchema
                {
                    Title = "data.go.kr API Key",
                    Description = "공공데이터포털(data.go.kr)에서 발급받은 인증키(serviceKey)",
                    MinLength = 1,
                },
            },
            Required = ["api_key"],
        };

        var request = new ElicitRequestParams
        {
            Message = "data.go.kr API 키(serviceKey)가 필요합니다. " +
                      "https://www.data.go.kr 에서 발급받은 인증키를 입력해주세요.",
            RequestedSchema = schema,
        };

        try
        {
            var result = await server.ElicitAsync(request, ct);
            if (!result.IsAccepted) return null;
            if (result.Content is null) return null;
            if (!result.Content.TryGetValue("api_key", out var value)) return null;

            var key = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            return string.IsNullOrWhiteSpace(key) ? null : key;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Client doesn't support elicitation, transport error, etc.
            // Soft-fail; tool will return the env-var hint message.
            return null;
        }
    }
}
