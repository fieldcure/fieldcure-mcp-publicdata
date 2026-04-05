using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace FieldCure.Mcp.PublicData.Kr.Services;

/// <summary>
/// Normalizes data.go.kr API responses by converting XML to JSON, stripping the
/// standard <c>response/header/body/items</c> wrapper, and trimming the result set
/// to a maximum number of items.
/// </summary>
static class ResponseNormalizer
{
    /// <summary>
    /// Normalizes a raw response body into a compact JSON string.
    /// </summary>
    /// <param name="content">Raw response body (XML or JSON).</param>
    /// <param name="contentType">HTTP Content-Type header value, used for encoding detection.</param>
    /// <param name="maxResults">Maximum number of items to include in the output.</param>
    /// <returns>A JSON string containing <c>totalCount</c> and <c>items</c>.</returns>
    public static string Normalize(string content, string? contentType, int maxResults)
    {
        if (string.IsNullOrWhiteSpace(content))
            return JsonSerializer.Serialize(new { error = "Empty response" });

        var trimmed = content.TrimStart();

        // Detect XML by content prefix
        if (trimmed.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("<response", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("<OpenAPI_ServiceResponse", StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeXml(content, maxResults);
        }

        // Assume JSON
        return NormalizeJson(content, maxResults);
    }

    /// <summary>
    /// Re-decodes raw bytes when the XML declares a non-UTF-8 encoding (e.g. EUC-KR).
    /// </summary>
    /// <param name="rawBytes">Original response bytes.</param>
    /// <param name="contentType">HTTP Content-Type header value.</param>
    /// <param name="maxResults">Maximum number of items to include.</param>
    /// <returns>A normalized JSON string.</returns>
    public static string NormalizeBytes(byte[] rawBytes, string? contentType, int maxResults)
    {
        var text = DetectAndDecode(rawBytes, contentType);
        return Normalize(text, contentType, maxResults);
    }

    /// <summary>
    /// Detects encoding from the XML declaration or Content-Type header and decodes bytes.
    /// </summary>
    static string DetectAndDecode(byte[] bytes, string? contentType)
    {
        // Quick peek at the first 100 bytes as ASCII to find encoding declaration
        var peek = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 200));

        if (peek.Contains("euc-kr", StringComparison.OrdinalIgnoreCase)
            || (contentType?.Contains("euc-kr", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            return Encoding.GetEncoding("euc-kr").GetString(bytes);
        }

        return Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// Converts a data.go.kr XML response to a normalized JSON string.
    /// </summary>
    static string NormalizeXml(string xml, int maxResults)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"XML parse error: {ex.Message}" });
        }

        if (doc.Root is null)
            return JsonSerializer.Serialize(new { error = "Empty XML document" });

        // Check for OpenAPI_ServiceResponse error format
        var errMsg = doc.Root.Element("cmmMsgHeader")?.Element("errMsg")?.Value;
        var returnAuthMsg = doc.Root.Element("cmmMsgHeader")?.Element("returnAuthMsg")?.Value;
        if (errMsg is not null)
        {
            return JsonSerializer.Serialize(new
            {
                error = errMsg,
                error_detail = returnAuthMsg,
            });
        }

        // Standard wrapper: response > header + body
        var header = doc.Root.Element("header");
        var resultCode = header?.Element("resultCode")?.Value;
        var resultMsg = header?.Element("resultMsg")?.Value;

        if (resultCode is not null and not "00" and not "0")
        {
            var mapped = ErrorCodeMapper.GetMessage(resultCode);
            return JsonSerializer.Serialize(new
            {
                error = mapped ?? resultMsg ?? $"Error code: {resultCode}",
                error_code = resultCode,
            });
        }

        var body = doc.Root.Element("body");
        if (body is null)
        {
            // Fallback: treat entire root as the data
            return ConvertElementToJson(doc.Root, maxResults);
        }

        var totalCount = (int?)body.Element("totalCount")
                         ?? (int?)body.Element("numOfRows");

        var itemsElement = body.Element("items");
        var items = new List<Dictionary<string, string>>();

        if (itemsElement is not null)
        {
            foreach (var item in itemsElement.Elements("item"))
            {
                var dict = new Dictionary<string, string>();
                foreach (var el in item.Elements())
                {
                    dict[el.Name.LocalName] = el.Value;
                }
                items.Add(dict);
            }
        }
        else
        {
            // Some APIs put items directly under body without wrapper
            foreach (var item in body.Elements("item"))
            {
                var dict = new Dictionary<string, string>();
                foreach (var el in item.Elements())
                {
                    dict[el.Name.LocalName] = el.Value;
                }
                items.Add(dict);
            }
        }

        // Single-item case: some APIs return a single item without a list wrapper
        if (items.Count == 0 && itemsElement is not null && itemsElement.HasElements
            && !itemsElement.Elements("item").Any())
        {
            var dict = new Dictionary<string, string>();
            foreach (var el in itemsElement.Elements())
            {
                dict[el.Name.LocalName] = el.Value;
            }
            if (dict.Count > 0)
                items.Add(dict);
        }

        var trimmed = items.Count > maxResults ? items.Take(maxResults).ToList() : items;

        return JsonSerializer.Serialize(new
        {
            totalCount = totalCount ?? items.Count,
            items = trimmed,
            truncated = items.Count > maxResults,
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Converts an XML element tree to a flat JSON representation.
    /// </summary>
    static string ConvertElementToJson(XElement element, int maxResults)
    {
        var dict = new Dictionary<string, object>();
        foreach (var child in element.Elements())
        {
            if (child.HasElements)
            {
                var nested = new Dictionary<string, string>();
                foreach (var grandChild in child.Elements())
                {
                    nested[grandChild.Name.LocalName] = grandChild.Value;
                }
                dict[child.Name.LocalName] = nested;
            }
            else
            {
                dict[child.Name.LocalName] = child.Value;
            }
        }

        return JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Passes through a JSON response, optionally trimming the items array.
    /// </summary>
    static string NormalizeJson(string json, int maxResults)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            // Not valid JSON; return as-is wrapped in a content field
            return JsonSerializer.Serialize(new { content = json });
        }

        if (root is null)
            return JsonSerializer.Serialize(new { error = "Null JSON response" });

        // Handle new-style API response: { currentCount, data, matchCount, page, perPage, totalCount }
        if (root is JsonObject obj && obj.ContainsKey("data") && obj["data"] is JsonArray dataArr)
        {
            var totalCount = obj["totalCount"]?.GetValue<int>() ?? dataArr.Count;
            var currentCount = obj["currentCount"]?.GetValue<int>();
            var page = obj["page"]?.GetValue<int>();
            var perPage = obj["perPage"]?.GetValue<int>();

            JsonArray trimmed;
            var wasTruncated = false;
            if (dataArr.Count > maxResults)
            {
                trimmed = new JsonArray();
                foreach (var item in dataArr.Take(maxResults))
                {
                    trimmed.Add(item?.DeepClone());
                }
                wasTruncated = true;
            }
            else
            {
                trimmed = dataArr.DeepClone().AsArray();
            }

            var result = new JsonObject
            {
                ["totalCount"] = totalCount,
                ["items"] = trimmed,
                ["truncated"] = wasTruncated,
            };

            if (page is not null)
                result["page"] = page;

            return result.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }

        // Handle old-style: { response: { header, body: { items: { item: [...] } } } }
        if (root is JsonObject obj2 && obj2.ContainsKey("response"))
        {
            var response = obj2["response"]?.AsObject();
            var header = response?["header"]?.AsObject();
            var resultCode = header?["resultCode"]?.GetValue<string>();

            if (resultCode is not null and not "00" and not "0")
            {
                var mapped = ErrorCodeMapper.GetMessage(resultCode);
                var resultMsg = header?["resultMsg"]?.GetValue<string>();
                return JsonSerializer.Serialize(new
                {
                    error = mapped ?? resultMsg ?? $"Error code: {resultCode}",
                    error_code = resultCode,
                });
            }

            var body = response?["body"]?.AsObject();
            if (body is not null)
            {
                var totalCount = body["totalCount"]?.GetValue<int>();
                var items = body["items"]?["item"];

                if (items is JsonArray arr)
                {
                    JsonArray trimmed;
                    var wasTruncated = false;
                    if (arr.Count > maxResults)
                    {
                        trimmed = new JsonArray();
                        foreach (var item in arr.Take(maxResults))
                        {
                            trimmed.Add(item?.DeepClone());
                        }
                        wasTruncated = true;
                    }
                    else
                    {
                        trimmed = arr.DeepClone().AsArray();
                    }

                    var result = new JsonObject
                    {
                        ["totalCount"] = totalCount ?? arr.Count,
                        ["items"] = trimmed,
                        ["truncated"] = wasTruncated,
                    };
                    return result.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                }

                // Single item (not array)
                if (items is JsonObject singleItem)
                {
                    var result = new JsonObject
                    {
                        ["totalCount"] = totalCount ?? 1,
                        ["items"] = new JsonArray { singleItem.DeepClone() },
                        ["truncated"] = false,
                    };
                    return result.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                }
            }
        }

        // Fallback: return pretty-printed
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }
}
