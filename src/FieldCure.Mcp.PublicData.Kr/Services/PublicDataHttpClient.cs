using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Web;

namespace FieldCure.Mcp.PublicData.Kr.Services;

/// <summary>
/// Centralized HTTP client for data.go.kr / api.odcloud.kr calls.
/// The API key (<c>serviceKey</c>) is supplied per call by the tool, resolved via
/// <see cref="ApiKeyResolver"/> following ADR-001 (env var → Elicitation). The client
/// enforces a response size limit, masks the API key in any error output, and throws
/// <see cref="InvalidApiKeyException"/> on upstream 401/403 so tools can invalidate
/// and retry.
/// </summary>
public sealed class PublicDataHttpClient
{
    /// <summary>
    /// Catalog API base URL on the new ODCloud platform.
    /// </summary>
    const string CatalogBaseUrl = "https://api.odcloud.kr/api/15077093/v1/open-data-list";

    readonly HttpClient _http;
    readonly int _maxResponseLength;

    /// <summary>
    /// Creates a new <see cref="PublicDataHttpClient"/>.
    /// </summary>
    /// <param name="timeoutSeconds">Per-request timeout in seconds.</param>
    /// <param name="maxResponseLength">Maximum response body length in characters.</param>
    public PublicDataHttpClient(int timeoutSeconds, int maxResponseLength)
    {
        _maxResponseLength = maxResponseLength;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };
    }

    /// <summary>
    /// Searches the open-data-list catalog for APIs matching the given keyword.
    /// </summary>
    /// <param name="apiKey">data.go.kr serviceKey resolved by the caller.</param>
    /// <param name="query">Search keyword (Korean or English).</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Results per page (max 100).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Raw JSON response body from the catalog API.</returns>
    public async Task<string> SearchCatalogAsync(
        string apiKey, string query, int page, int pageSize, CancellationToken ct)
    {
        var qs = HttpUtility.ParseQueryString(string.Empty);
        qs["serviceKey"] = apiKey;
        qs["page"] = page.ToString();
        qs["perPage"] = Math.Clamp(pageSize, 1, 100).ToString();
        qs["cond[list_title::LIKE]"] = query;

        var url = $"{CatalogBaseUrl}?{qs}";
        return await GetStringAsync(url, apiKey, ct);
    }

    /// <summary>
    /// Retrieves all operations for a specific service from the catalog.
    /// </summary>
    /// <param name="apiKey">data.go.kr serviceKey resolved by the caller.</param>
    /// <param name="serviceId">The <c>list_id</c> value from a previous search.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Raw JSON response body containing operation-level details.</returns>
    public async Task<string> GetServiceDetailAsync(string apiKey, string serviceId, CancellationToken ct)
    {
        var qs = HttpUtility.ParseQueryString(string.Empty);
        qs["serviceKey"] = apiKey;
        qs["perPage"] = "100";
        qs["cond[list_id::EQ]"] = serviceId;

        var url = $"{CatalogBaseUrl}?{qs}";
        return await GetStringAsync(url, apiKey, ct);
    }

    /// <summary>
    /// Calls an arbitrary public-data API endpoint, injecting the <c>serviceKey</c>
    /// parameter and normalizing the response.
    /// </summary>
    /// <param name="apiKey">data.go.kr serviceKey resolved by the caller.</param>
    /// <param name="uri">Target endpoint URI (already validated by <see cref="DomainWhitelist"/>).</param>
    /// <param name="queryParams">Additional query parameters to append.</param>
    /// <param name="maxResults">Maximum items to include after normalization.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A normalized JSON string ready for the LLM.</returns>
    public async Task<string> CallApiAsync(
        string apiKey, Uri uri, Dictionary<string, string>? queryParams, int maxResults, CancellationToken ct)
    {
        var builder = new UriBuilder(uri);
        var qs = HttpUtility.ParseQueryString(builder.Query);

        // Inject serviceKey
        qs["serviceKey"] = apiKey;

        if (queryParams is not null)
        {
            foreach (var (key, value) in queryParams)
            {
                if (!key.Equals("serviceKey", StringComparison.OrdinalIgnoreCase))
                    qs[key] = value;
            }
        }

        builder.Query = qs.ToString();

        var response = await _http.GetAsync(builder.Uri, ct);
        ThrowIfInvalidKey(response);

        var contentType = response.Content.Headers.ContentType?.ToString();
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);

        if (bytes.Length == 0)
            return JsonSerializer.Serialize(new { error = "Empty response from API" });

        // Truncate oversized responses at byte level before decoding
        if (bytes.Length > _maxResponseLength * 2)
            bytes = bytes[..(_maxResponseLength * 2)];

        var normalized = ResponseNormalizer.NormalizeBytes(bytes, contentType, maxResults);

        // data.go.kr often reports an invalid serviceKey as HTTP 200 with an XML/JSON
        // auth error in the body (e.g. resultCode=22 SERVICE_KEY_IS_NOT_REGISTERED_ERROR).
        // Translate those into InvalidApiKeyException so the tool retry/re-elicit path
        // fires the same way it does on HTTP 401/403.
        ThrowIfBodySignalsInvalidKey(normalized);

        return normalized;
    }

    /// <summary>
    /// Fetches a URL as a string, enforcing the response length limit and masking the API key.
    /// </summary>
    async Task<string> GetStringAsync(string url, string apiKey, CancellationToken ct)
    {
        var response = await _http.GetAsync(url, ct);
        ThrowIfInvalidKey(response);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);

        if (body.Length > _maxResponseLength)
            body = body[.._maxResponseLength];

        return MaskApiKey(body, apiKey);
    }

    /// <summary>
    /// Throws <see cref="InvalidApiKeyException"/> when the upstream status code indicates
    /// the supplied <c>serviceKey</c> was rejected (401/403).
    /// </summary>
    static void ThrowIfInvalidKey(HttpResponseMessage response)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new InvalidApiKeyException((int)response.StatusCode, response.ReasonPhrase);
    }

    /// <summary>
    /// Inspects a <see cref="ResponseNormalizer"/> output envelope for data.go.kr auth-error
    /// signals (HTTP 200 with a body that reports <c>resultCode=22</c> or the OpenAPI
    /// <c>SERVICE_KEY_IS_NOT_REGISTERED_ERROR</c> message) and throws
    /// <see cref="InvalidApiKeyException"/> so tools trigger the invalidate+re-elicit path.
    /// </summary>
    static void ThrowIfBodySignalsInvalidKey(string normalizedJson)
    {
        if (string.IsNullOrWhiteSpace(normalizedJson)) return;

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(normalizedJson);
        }
        catch (JsonException)
        {
            return;
        }

        if (root is not JsonObject obj) return;

        // Pattern 1: standard header format surfaces error_code explicitly.
        if (obj["error_code"]?.GetValue<string>() is { } code && code == "22")
            throw new InvalidApiKeyException(200, "data.go.kr resultCode=22 (SERVICE_KEY_IS_NOT_REGISTERED)");

        // Pattern 2: OpenAPI_ServiceResponse errMsg comes through as the "error" field.
        if (obj["error"]?.GetValue<string>() is { } err
            && (err.Contains("SERVICE_KEY_IS_NOT_REGISTERED", StringComparison.OrdinalIgnoreCase)
                || err.Contains("UNAUTHORIZED_KEY", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidApiKeyException(200, err);
        }
    }

    /// <summary>
    /// Replaces the raw API key with asterisks in output strings.
    /// </summary>
    static string MaskApiKey(string text, string apiKey) =>
        text.Replace(apiKey, "***", StringComparison.Ordinal);
}
