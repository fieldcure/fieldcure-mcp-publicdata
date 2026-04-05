using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
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
    /// Shared JSON serialization options.
    /// </summary>
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Searches Korean public data APIs on data.go.kr by keyword.
    /// </summary>
    [McpServerTool(Name = "discover_api")]
    [Description(
        "Search Korean public data APIs on data.go.kr by keyword. " +
        "Returns API names, descriptions, providers, and service URLs. " +
        "Use this when the user asks about Korean government data.")]
    public static async Task<string> DiscoverApi(
        PublicDataHttpClient client,
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
            var rawJson = await client.SearchCatalogAsync(query, page, fetchSize, cancellationToken);

            return FormatResults(rawJson, pageSize);
        }
        catch (OperationCanceledException)
        {
            return JsonSerializer.Serialize(new { error = "Request timed out." }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    /// <summary>
    /// De-duplicates catalog entries by list_id and formats the response.
    /// </summary>
    static string FormatResults(string rawJson, int pageSize)
    {
        var root = JsonNode.Parse(rawJson);
        if (root is null)
            return JsonSerializer.Serialize(new { error = "Failed to parse catalog response" }, JsonOptions);

        var totalCount = root["totalCount"]?.GetValue<int>() ?? 0;
        var data = root["data"]?.AsArray();

        if (data is null || data.Count == 0)
        {
            return JsonSerializer.Serialize(new
            {
                total_count = totalCount,
                results = Array.Empty<object>(),
            }, JsonOptions);
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
        }, JsonOptions);
    }
}
