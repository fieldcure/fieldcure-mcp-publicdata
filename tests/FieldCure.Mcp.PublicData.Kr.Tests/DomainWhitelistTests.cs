using FieldCure.Mcp.PublicData.Kr.Services;

namespace FieldCure.Mcp.PublicData.Kr.Tests;

[TestClass]
public class DomainWhitelistTests
{
    [TestMethod]
    [DataRow("https://apis.data.go.kr/1471000/MdcinClincTestInfoService02/getMdcinClincTestInfoList02")]
    [DataRow("https://api.odcloud.kr/api/15077093/v1/open-data-list")]
    [DataRow("https://api.data.go.kr/some/endpoint")]
    [DataRow("https://openapi.data.go.kr/some/endpoint")]
    [DataRow("https://www.law.go.kr/DRF/lawSearch.do")]
    [DataRow("https://open.neis.go.kr/hub/schoolInfo")]
    [DataRow("http://apis.data.go.kr/plain-http")]
    public void AllowedHosts_ReturnsUri(string url)
    {
        var (uri, error) = DomainWhitelist.Validate(url);

        Assert.IsNotNull(uri);
        Assert.IsNull(error);
    }

    [TestMethod]
    [DataRow("https://evil.com/steal?key=123")]
    [DataRow("https://google.com")]
    [DataRow("https://192.168.1.1/admin")]
    [DataRow("https://localhost/internal")]
    public void BlockedHosts_ReturnsError(string url)
    {
        var (uri, error) = DomainWhitelist.Validate(url);

        Assert.IsNull(uri);
        Assert.IsNotNull(error);
        Assert.IsTrue(error!.Contains("not in the allowed list"));
    }

    [TestMethod]
    [DataRow("ftp://apis.data.go.kr/file")]
    [DataRow("file:///etc/passwd")]
    public void NonHttpScheme_ReturnsError(string url)
    {
        var (uri, error) = DomainWhitelist.Validate(url);

        Assert.IsNull(uri);
        Assert.IsNotNull(error);
        Assert.IsTrue(error!.Contains("Only http/https"));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow(null)]
    public void EmptyUrl_ReturnsError(string? url)
    {
        var (uri, error) = DomainWhitelist.Validate(url!);

        Assert.IsNull(uri);
        Assert.IsNotNull(error);
    }

    [TestMethod]
    [DataRow("not-a-url")]
    [DataRow("://missing-scheme")]
    public void InvalidUrl_ReturnsError(string url)
    {
        var (uri, error) = DomainWhitelist.Validate(url);

        Assert.IsNull(uri);
        Assert.IsNotNull(error);
    }
}
