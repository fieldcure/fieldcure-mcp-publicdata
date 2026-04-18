namespace FieldCure.Mcp.PublicData.Kr.Services;

/// <summary>
/// Thrown by <see cref="PublicDataHttpClient"/> when the upstream data.go.kr endpoint
/// reports that the supplied <c>serviceKey</c> is invalid — either via HTTP 401/403 or
/// via a normalized body signal (<c>resultCode=22</c>, <c>SERVICE_KEY_IS_NOT_REGISTERED_ERROR</c>,
/// <c>UNAUTHORIZED_KEY</c>). Tools catch this exception to trigger
/// <see cref="ApiKeyResolver.Invalidate"/> and a single retry per tool invocation.
/// </summary>
public sealed class InvalidApiKeyException : Exception
{
    /// <summary>
    /// Creates a new exception carrying the upstream status code that triggered detection.
    /// May be 401/403 (HTTP-level) or 200 (body-signal detection at HTTP 200).
    /// </summary>
    public InvalidApiKeyException(int statusCode, string? reason = null)
        : base($"data.go.kr rejected the API key (HTTP {statusCode})" +
               (string.IsNullOrEmpty(reason) ? "." : $": {reason}"))
    {
        StatusCode = statusCode;
    }

    /// <summary>
    /// HTTP status code that triggered invalidation (401, 403, or 200 with body signal).
    /// </summary>
    public int StatusCode { get; }
}
