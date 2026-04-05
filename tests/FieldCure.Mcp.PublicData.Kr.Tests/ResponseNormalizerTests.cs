using FieldCure.Mcp.PublicData.Kr.Services;
using System.Text.Json;

namespace FieldCure.Mcp.PublicData.Kr.Tests;

[TestClass]
public class ResponseNormalizerTests
{
    [TestMethod]
    public void XmlWithItemsArray_ExtractsItemsAndTotalCount()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <response>
              <header>
                <resultCode>00</resultCode>
                <resultMsg>NORMAL SERVICE.</resultMsg>
              </header>
              <body>
                <items>
                  <item>
                    <stationName>종로구</stationName>
                    <pm10Value>45</pm10Value>
                  </item>
                  <item>
                    <stationName>중구</stationName>
                    <pm10Value>38</pm10Value>
                  </item>
                </items>
                <totalCount>2</totalCount>
              </body>
            </response>
            """;

        var result = ResponseNormalizer.Normalize(xml, "application/xml", 100);
        var doc = JsonDocument.Parse(result);

        Assert.AreEqual(2, doc.RootElement.GetProperty("totalCount").GetInt32());
        Assert.AreEqual(2, doc.RootElement.GetProperty("items").GetArrayLength());
        Assert.AreEqual("종로구", doc.RootElement.GetProperty("items")[0].GetProperty("stationName").GetString());
    }

    [TestMethod]
    public void XmlWithMaxResults_TrimsItems()
    {
        var xml = """
            <response>
              <header><resultCode>00</resultCode></header>
              <body>
                <items>
                  <item><name>A</name></item>
                  <item><name>B</name></item>
                  <item><name>C</name></item>
                </items>
                <totalCount>3</totalCount>
              </body>
            </response>
            """;

        var result = ResponseNormalizer.Normalize(xml, null, 2);
        var doc = JsonDocument.Parse(result);

        Assert.AreEqual(3, doc.RootElement.GetProperty("totalCount").GetInt32());
        Assert.AreEqual(2, doc.RootElement.GetProperty("items").GetArrayLength());
        Assert.IsTrue(doc.RootElement.GetProperty("truncated").GetBoolean());
    }

    [TestMethod]
    public void XmlWithErrorCode_ReturnsErrorMessage()
    {
        var xml = """
            <response>
              <header>
                <resultCode>20</resultCode>
                <resultMsg>ACCESS_DENIED</resultMsg>
              </header>
              <body/>
            </response>
            """;

        var result = ResponseNormalizer.Normalize(xml, null, 100);
        var doc = JsonDocument.Parse(result);

        Assert.IsTrue(doc.RootElement.GetProperty("error").GetString()!.Contains("활용신청"));
        Assert.AreEqual("20", doc.RootElement.GetProperty("error_code").GetString());
    }

    [TestMethod]
    public void NewStyleJson_ExtractsDataArray()
    {
        var json = """
            {
              "currentCount": 2,
              "data": [
                { "list_id": "1", "title": "API A" },
                { "list_id": "2", "title": "API B" }
              ],
              "matchCount": 2,
              "page": 1,
              "perPage": 10,
              "totalCount": 2
            }
            """;

        var result = ResponseNormalizer.Normalize(json, "application/json", 100);
        var doc = JsonDocument.Parse(result);

        Assert.AreEqual(2, doc.RootElement.GetProperty("totalCount").GetInt32());
        Assert.AreEqual(2, doc.RootElement.GetProperty("items").GetArrayLength());
    }

    [TestMethod]
    public void NewStyleJson_TrimsToMaxResults()
    {
        var json = """
            {
              "data": [
                { "id": "1" },
                { "id": "2" },
                { "id": "3" }
              ],
              "totalCount": 3
            }
            """;

        var result = ResponseNormalizer.Normalize(json, "application/json", 1);
        var doc = JsonDocument.Parse(result);

        Assert.AreEqual(1, doc.RootElement.GetProperty("items").GetArrayLength());
        Assert.IsTrue(doc.RootElement.GetProperty("truncated").GetBoolean());
    }

    [TestMethod]
    public void EmptyContent_ReturnsError()
    {
        var result = ResponseNormalizer.Normalize("", null, 100);
        var doc = JsonDocument.Parse(result);

        Assert.IsTrue(doc.RootElement.TryGetProperty("error", out _));
    }

    [TestMethod]
    public void XmlOpenApiServiceResponse_ReturnsError()
    {
        var xml = """
            <OpenAPI_ServiceResponse>
              <cmmMsgHeader>
                <errMsg>SERVICE ERROR</errMsg>
                <returnAuthMsg>인증 실패</returnAuthMsg>
              </cmmMsgHeader>
            </OpenAPI_ServiceResponse>
            """;

        var result = ResponseNormalizer.Normalize(xml, null, 100);
        var doc = JsonDocument.Parse(result);

        Assert.AreEqual("SERVICE ERROR", doc.RootElement.GetProperty("error").GetString());
    }

    [TestMethod]
    public void OldStyleJsonResponse_ExtractsItems()
    {
        var json = """
            {
              "response": {
                "header": { "resultCode": "00", "resultMsg": "NORMAL SERVICE." },
                "body": {
                  "items": {
                    "item": [
                      { "name": "A" },
                      { "name": "B" }
                    ]
                  },
                  "totalCount": 2
                }
              }
            }
            """;

        var result = ResponseNormalizer.Normalize(json, "application/json", 100);
        var doc = JsonDocument.Parse(result);

        Assert.AreEqual(2, doc.RootElement.GetProperty("totalCount").GetInt32());
        Assert.AreEqual(2, doc.RootElement.GetProperty("items").GetArrayLength());
    }
}
