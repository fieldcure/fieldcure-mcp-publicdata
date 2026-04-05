using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using FieldCure.Mcp.PublicData.Kr.Services;
using ModelContextProtocol.Server;

namespace FieldCure.Mcp.PublicData.Kr.Tools;

/// <summary>
/// MCP tool that retrieves the operations, request parameters, and response fields
/// for a specific data.go.kr API service.
/// </summary>
[McpServerToolType]
public static class DescribeApiTool
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
    /// Gets the request parameters and response schema of a specific data.go.kr API.
    /// </summary>
    [McpServerTool(Name = "describe_api")]
    [Description(
        "Get the request parameters and response schema of a specific data.go.kr API. " +
        "Use the serviceId from discover_api results.")]
    public static async Task<string> DescribeApi(
        PublicDataHttpClient client,
        [Description("Service ID (list_id) from discover_api results")]
        string serviceId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(serviceId);

            var rawJson = await client.GetServiceDetailAsync(serviceId, cancellationToken);
            return FormatSchema(rawJson, serviceId);
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
    /// Extracts operation-level details from the catalog response and structures them
    /// into a schema description suitable for LLM consumption.
    /// </summary>
    static string FormatSchema(string rawJson, string serviceId)
    {
        var root = JsonNode.Parse(rawJson);
        if (root is null)
            return JsonSerializer.Serialize(new { error = "Failed to parse catalog response" }, JsonOptions);

        var data = root["data"]?.AsArray();
        if (data is null || data.Count == 0)
        {
            return JsonSerializer.Serialize(new
            {
                error = $"No API found with serviceId '{serviceId}'. " +
                        "Use discover_api to search for available APIs.",
            }, JsonOptions);
        }

        // All entries share the same list-level metadata
        var first = data[0]!;
        var serviceName = first["list_title"]?.GetValue<string>();
        var baseUrl = first["end_point_url"]?.GetValue<string>();
        var guideUrl = first["guide_url"]?.GetValue<string>();
        var provider = first["org_nm"]?.GetValue<string>();
        var dataFormat = first["data_format"]?.GetValue<string>();

        // Each entry represents one operation
        var operations = new List<object>();
        foreach (var item in data)
        {
            if (item is null) continue;

            var operationName = item["operation_nm"]?.GetValue<string>();
            var operationUrl = item["operation_url"]?.GetValue<string>();
            var requestParamsKr = item["request_param_nm"]?.GetValue<string>();
            var requestParamsEn = item["request_param_nm_en"]?.GetValue<string>();
            var responseParamsKr = item["response_param_nm"]?.GetValue<string>();
            var responseParamsEn = item["response_param_nm_en"]?.GetValue<string>();

            operations.Add(new
            {
                name = operationName,
                url = operationUrl,
                requestParameters = ParseParamPairs(requestParamsKr, requestParamsEn),
                responseFields = ParseParamPairs(responseParamsKr, responseParamsEn),
            });
        }

        return JsonSerializer.Serialize(new
        {
            serviceId,
            serviceName,
            provider,
            baseUrl,
            dataFormat,
            guideUrl,
            operations,
        }, JsonOptions);
    }

    /// <summary>
    /// Splits comma-separated Korean and English parameter name strings into a paired list.
    /// </summary>
    static List<object>? ParseParamPairs(string? krNames, string? enNames)
    {
        if (string.IsNullOrWhiteSpace(krNames) && string.IsNullOrWhiteSpace(enNames))
            return null;

        var krList = SplitParams(krNames);
        var enList = SplitParams(enNames);
        var maxLen = Math.Max(krList.Count, enList.Count);

        if (maxLen == 0) return null;

        var result = new List<object>();
        for (var i = 0; i < maxLen; i++)
        {
            var kr = i < krList.Count ? krList[i] : null;
            var en = i < enList.Count ? enList[i] : null;

            result.Add(new
            {
                name = en ?? kr,
                description = kr != en ? kr : null,
            });
        }

        return result;
    }

    /// <summary>
    /// Splits a comma-separated parameter name string, trimming whitespace.
    /// </summary>
    static List<string> SplitParams(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }
}
