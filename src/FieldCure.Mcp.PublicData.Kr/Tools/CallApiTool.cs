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
        "Call a Korean public data API with automatic serviceKey injection. " +
        "WORKFLOW: always discover_api → describe_api → call_api. Do NOT call this tool " +
        "with a serviceId from discover_api; call_api takes a full 'url' string returned " +
        "by describe_api under operations[].url, NOT a serviceId. " +
        "The 'params' argument is a JSON STRING (not an object), " +
        "e.g. params=\"{\\\"pageNo\\\":\\\"1\\\",\\\"numOfRows\\\":\\\"5\\\"}\". " +
        "If the call fails with ACCESS_DENIED, the user needs to apply for access to this " +
        "specific API at data.go.kr.")]
    public static async Task<string> CallApi(
        McpServer server,
        PublicDataHttpClient client,
        ApiKeyResolver resolver,
        [Description(
            "Full endpoint URL from describe_api's operations[].url. " +
            "Do NOT pass a serviceId / service_id from discover_api — that is for describe_api, " +
            "not for this tool. A valid url looks like " +
            "'http://apis.data.go.kr/1471000/MdcinClincTestInfoService02/getMdcinClincTestInfoList02'.")]
        string url,
        [Description(
            "Query parameters as a JSON STRING (serialized object, not a raw object), " +
            "with string values only. Example: " +
            "'{\"pageNo\":\"1\",\"numOfRows\":\"5\",\"type\":\"json\"}'. " +
            "Parameter names come from describe_api's request_parameters list — do not guess.")]
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

            return await KeyedCall.RunAsync(
                server,
                resolver,
                apiKey => client.CallApiAsync(apiKey, uri, queryParams, maxResults, cancellationToken),
                cancellationToken);
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
