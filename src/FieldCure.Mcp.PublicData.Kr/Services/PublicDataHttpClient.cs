using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Web;

namespace FieldCure.Mcp.PublicData.Kr.Services;

/// <summary>
/// Centralized HTTP client for data.go.kr / api.odcloud.kr calls.
/// Automatically injects the <c>serviceKey</c> query parameter, enforces a response
/// size limit, and masks the API key in any error output.
/// </summary>
public sealed class PublicDataHttpClient
{
    /// <summary>
    /// Catalog API base URL on the new ODCloud platform.
    /// </summary>
    const string CatalogBaseUrl = "https://api.odcloud.kr/api/15077093/v1/open-data-list";

    readonly HttpClient _http;
    readonly string _apiKey;
    readonly int _maxResponseLength;

    /// <summary>
    /// Creates a new <see cref="PublicDataHttpClient"/>.
    /// </summary>
    /// <param name="apiKey">data.go.kr API authentication key.</param>
    /// <param name="timeoutSeconds">Per-request timeout in seconds.</param>
    /// <param name="maxResponseLength">Maximum response body length in characters.</param>
    public PublicDataHttpClient(string apiKey, int timeoutSeconds, int maxResponseLength)
    {
        _apiKey = apiKey;
        _maxResponseLength = maxResponseLength;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };
    }

    /// <summary>
    /// Searches the open-data-list catalog for APIs matching the given keyword.
    /// </summary>
    /// <param name="query">Search keyword (Korean or English).</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Results per page (max 100).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Raw JSON response body from the catalog API.</returns>
    public async Task<string> SearchCatalogAsync(
        string query, int page, int pageSize, CancellationToken ct)
    {
        var qs = HttpUtility.ParseQueryString(string.Empty);
        qs["serviceKey"] = _apiKey;
        qs["page"] = page.ToString();
        qs["perPage"] = Math.Clamp(pageSize, 1, 100).ToString();
        qs["cond[list_title::LIKE]"] = query;

        var url = $"{CatalogBaseUrl}?{qs}";
        return await GetStringAsync(url, ct);
    }

    /// <summary>
    /// Retrieves all operations for a specific service from the catalog.
    /// </summary>
    /// <param name="serviceId">The <c>list_id</c> value from a previous search.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Raw JSON response body containing operation-level details.</returns>
    public async Task<string> GetServiceDetailAsync(string serviceId, CancellationToken ct)
    {
        var qs = HttpUtility.ParseQueryString(string.Empty);
        qs["serviceKey"] = _apiKey;
        qs["perPage"] = "100";
        qs["cond[list_id::EQ]"] = serviceId;

        var url = $"{CatalogBaseUrl}?{qs}";
        return await GetStringAsync(url, ct);
    }

    /// <summary>
    /// Calls an arbitrary public-data API endpoint, injecting the <c>serviceKey</c>
    /// parameter and normalizing the response.
    /// </summary>
    /// <param name="uri">Target endpoint URI (already validated by <see cref="DomainWhitelist"/>).</param>
    /// <param name="queryParams">Additional query parameters to append.</param>
    /// <param name="maxResults">Maximum items to include after normalization.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A normalized JSON string ready for the LLM.</returns>
    public async Task<string> CallApiAsync(
        Uri uri, Dictionary<string, string>? queryParams, int maxResults, CancellationToken ct)
    {
        var builder = new UriBuilder(uri);
        var qs = HttpUtility.ParseQueryString(builder.Query);

        // Inject serviceKey
        qs["serviceKey"] = _apiKey;

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
        var contentType = response.Content.Headers.ContentType?.ToString();
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);

        if (bytes.Length == 0)
            return JsonSerializer.Serialize(new { error = "Empty response from API" });

        // Truncate oversized responses at byte level before decoding
        if (bytes.Length > _maxResponseLength * 2)
            bytes = bytes[..(_maxResponseLength * 2)];

        return ResponseNormalizer.NormalizeBytes(bytes, contentType, maxResults);
    }

    /// <summary>
    /// Fetches a URL as a string, enforcing the response length limit.
    /// </summary>
    async Task<string> GetStringAsync(string url, CancellationToken ct)
    {
        var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);

        if (body.Length > _maxResponseLength)
            body = body[.._maxResponseLength];

        return MaskApiKey(body);
    }

    /// <summary>
    /// Replaces the raw API key with asterisks in output strings.
    /// </summary>
    string MaskApiKey(string text) =>
        text.Replace(_apiKey, "***", StringComparison.Ordinal);
}
