using System.ComponentModel;
using System.Text.Json;
using FieldCure.Mcp.PublicData.Kr.Services;
using ModelContextProtocol.Server;

namespace FieldCure.Mcp.PublicData.Kr.Tools;

/// <summary>
/// MCP tool that proxies HTTP calls to Korean public data APIs with automatic
/// <c>serviceKey</c> injection, XML-to-JSON conversion, and error mapping.
/// </summary>
[McpServerToolType]
public static class CallApiTool
{
    /// <summary>
    /// Calls a Korean public data API with automatic serviceKey injection.
    /// </summary>
    [McpServerTool(Name = "call_api")]
    [Description(
        "Call a Korean public data API. The API key (serviceKey) is automatically injected. " +
        "If the call fails with ACCESS_DENIED, the user needs to apply for access to this " +
        "specific API at data.go.kr.")]
    public static async Task<string> CallApi(
        PublicDataHttpClient client,
        [Description("Full endpoint URL from describe_api results")]
        string url,
        [Description("Query parameters as JSON object, e.g. {\"stationName\": \"종로구\", \"dataTerm\": \"DAILY\"}")]
        string? @params = null,
        [Description("Max items to return (default: 20, prevents context overflow)")]
        int maxResults = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate URL against domain whitelist
            var (uri, urlError) = DomainWhitelist.Validate(url);
            if (uri is null)
                return JsonSerializer.Serialize(new { error = urlError }, McpJson.Options);

            // Parse query params from JSON string
            Dictionary<string, string>? queryParams = null;
            if (@params is not null)
            {
                try
                {
                    queryParams = JsonSerializer.Deserialize<Dictionary<string, string>>(@params);
                }
                catch (JsonException)
                {
                    return JsonSerializer.Serialize(
                        new { error = "Invalid params JSON. Expected a flat object with string values." },
                        McpJson.Options);
                }
            }

            maxResults = Math.Clamp(maxResults, 1, 1000);

            var result = await client.CallApiAsync(uri, queryParams, maxResults, cancellationToken);
            return result;
        }
        catch (OperationCanceledException)
        {
            return JsonSerializer.Serialize(new { error = "Request timed out." }, McpJson.Options);
        }
        catch (HttpRequestException ex)
        {
            return JsonSerializer.Serialize(new { error = $"HTTP error: {ex.Message}" }, McpJson.Options);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, McpJson.Options);
        }
    }
}
