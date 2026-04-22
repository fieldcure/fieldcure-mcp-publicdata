using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using FieldCure.Mcp.PublicData.Kr.Services;
using ModelContextProtocol.Server;

namespace FieldCure.Mcp.PublicData.Kr.Tools;

/// <summary>
/// MCP tool that searches the data.go.kr catalog for public data APIs.
/// Results are de-duplicated by <c>list_id</c> so each service appears once.
/// </summary>
[McpServerToolType]
public static class DiscoverApiTool
{
    /// <summary>
    /// Searches Korean public data APIs on data.go.kr by keyword.
    /// </summary>
    [McpServerTool(Name = "discover_api")]
    [Description(
        "Search Korean public data APIs on data.go.kr by keyword. " +
        "Returns API names, descriptions, providers, and serviceIds. " +
        "Use this when the user asks about Korean government data. " +
        "WORKFLOW: the returned serviceId is the input for describe_api (not call_api). " +
        "Always follow up with describe_api to get the actual endpoint url and parameter " +
        "schema before calling call_api.")]
    public static async Task<string> DiscoverApi(
        McpServer server,
        PublicDataHttpClient client,
        ApiKeyResolver resolver,
        [Description("Search keyword in Korean or English (e.g., '미세먼지', '부동산', '사업자')")]
        string query,
        [Description("Page number (default: 1)")]
        int page = 1,
        [Description("Results per page (default: 10, max: 50)")]
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            pageSize = Math.Clamp(pageSize, 1, 50);
            page = Math.Max(page, 1);

            // Fetch more than requested to account for duplicates after de-duplication
            var fetchSize = Math.Min(pageSize * 3, 100);

            var rawJson = await KeyedCall.RunAsync(
                server,
                resolver,
                apiKey => client.SearchCatalogAsync(apiKey, query, page, fetchSize, cancellationToken),
                cancellationToken);

            // If KeyedCall surfaced a soft-fail/invalid-key error, pass it through as-is.
            if (LooksLikeErrorEnvelope(rawJson))
                return rawJson;

            return FormatResults(rawJson, pageSize);
        }
        catch (OperationCanceledException)
        {
            return JsonSerializer.Serialize(new { error = "Request timed out." }, McpJson.Options);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, McpJson.Options);
        }
    }

    /// <summary>
    /// Heuristic: does this JSON look like a <c>{ "error": "..." }</c> envelope produced
    /// by the soft-fail / invalid-key retry path?
    /// </summary>
    static bool LooksLikeErrorEnvelope(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            var root = JsonNode.Parse(json);
            return root?["error"] is not null && root["data"] is null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// De-duplicates catalog entries by list_id and formats the response.
    /// </summary>
    static string FormatResults(string rawJson, int pageSize)
    {
        var root = JsonNode.Parse(rawJson);
        if (root is null)
            return JsonSerializer.Serialize(new { error = "Failed to parse catalog response" }, McpJson.Options);

        var totalCount = root["totalCount"]?.GetValue<int>() ?? 0;
        var data = root["data"]?.AsArray();

        if (data is null || data.Count == 0)
        {
            return JsonSerializer.Serialize(new
            {
                total_count = totalCount,
                results = Array.Empty<object>(),
            }, McpJson.Options);
        }

        // De-duplicate by list_id — keep first occurrence
        var seen = new HashSet<string>();
        var results = new List<object>();

        foreach (var item in data)
        {
            if (item is null) continue;

            var listId = item["list_id"]?.GetValue<string>();
            if (listId is null || !seen.Add(listId)) continue;

            results.Add(new
            {
                serviceId = listId,
                serviceName = item["list_title"]?.GetValue<string>(),
                description = item["desc"]?.GetValue<string>(),
                provider = item["org_nm"]?.GetValue<string>(),
                serviceUrl = item["end_point_url"]?.GetValue<string>(),
                dataType = item["data_format"]?.GetValue<string>(),
                keywords = item["keywords"]?.GetValue<string>(),
            });

            if (results.Count >= pageSize) break;
        }

        return JsonSerializer.Serialize(new
        {
            total_count = totalCount,
            results,
        }, McpJson.Options);
    }
}
