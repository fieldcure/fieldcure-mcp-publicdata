namespace FieldCure.Mcp.PublicData.Kr.Services;

/// <summary>
/// Thrown by <see cref="PublicDataHttpClient"/> when the upstream data.go.kr endpoint
/// reports that the supplied <c>serviceKey</c> is invalid (HTTP 401/403).
/// Tools catch this exception to trigger <see cref="ApiKeyResolver.Invalidate"/>
/// and a single retry per tool invocation.
/// </summary>
public sealed class InvalidApiKeyException : Exception
{
    /// <summary>
    /// Creates a new exception carrying the upstream status code.
    /// </summary>
    public InvalidApiKeyException(int statusCode, string? reason = null)
        : base($"data.go.kr rejected the API key (HTTP {statusCode})" +
               (string.IsNullOrEmpty(reason) ? "." : $": {reason}"))
    {
        StatusCode = statusCode;
    }

    /// <summary>
    /// HTTP status code that triggered invalidation (typically 401 or 403).
    /// </summary>
    public int StatusCode { get; }
}
