using FieldCure.Mcp.PublicData.Kr.Services;

namespace FieldCure.Mcp.PublicData.Kr.Tests;

[TestClass]
public class ErrorCodeMapperTests
{
    [TestMethod]
    [DataRow("00")]
    [DataRow("0")]
    [DataRow(null)]
    public void SuccessCodes_ReturnNull(string? code)
    {
        Assert.IsNull(ErrorCodeMapper.GetMessage(code));
    }

    [TestMethod]
    public void Code12_ReturnsServiceNotFound()
    {
        var msg = ErrorCodeMapper.GetMessage("12");

        Assert.IsNotNull(msg);
        Assert.IsTrue(msg.Contains("discover_api"));
    }

    [TestMethod]
    public void Code20_ReturnsAccessDeniedWithServiceId()
    {
        var msg = ErrorCodeMapper.GetMessage("20", "15073861");

        Assert.IsNotNull(msg);
        Assert.IsTrue(msg.Contains("활용신청"));
        Assert.IsTrue(msg.Contains("15073861"));
    }

    [TestMethod]
    public void Code22_ReturnsKeyNotRegistered()
    {
        var msg = ErrorCodeMapper.GetMessage("22");

        Assert.IsNotNull(msg);
        Assert.IsTrue(msg.Contains("API 키"));
    }

    [TestMethod]
    public void Code30_ReturnsTrafficExceeded()
    {
        var msg = ErrorCodeMapper.GetMessage("30");

        Assert.IsNotNull(msg);
        Assert.IsTrue(msg.Contains("호출 한도"));
    }

    [TestMethod]
    public void Code31_ReturnsUnregisteredIp()
    {
        var msg = ErrorCodeMapper.GetMessage("31");

        Assert.IsNotNull(msg);
        Assert.IsTrue(msg.Contains("IP"));
    }

    [TestMethod]
    public void UnknownCode_ReturnsGenericMessage()
    {
        var msg = ErrorCodeMapper.GetMessage("99");

        Assert.IsNotNull(msg);
        Assert.IsTrue(msg.Contains("99"));
    }
}
