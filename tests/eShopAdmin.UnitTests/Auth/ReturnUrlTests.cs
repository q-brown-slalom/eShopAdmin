using eShopAdmin.Auth;

namespace eShopAdmin.UnitTests.Auth;

[TestClass]
public class ReturnUrlTests
{
    [TestMethod]
    [DataRow("/", true)]
    [DataRow("/rules", true)]
    [DataRow("/rules/ORD-001?x=1", true)]
    [DataRow(null, false)]
    [DataRow("", false)]
    [DataRow("rules", false)]
    [DataRow("//evil.example", false)]
    [DataRow("/\\evil.example", false)]
    [DataRow("https://evil.example/", false)]
    [DataRow("/\t/evil.example", false)]
    [DataRow("/\n/evil", false)]
    [DataRow("/\r/evil", false)]
    [DataRow("/rules\t", false)]
    [DataRow(" /rules", false)]
    [DataRow(" //evil.example", false)]
    [DataRow("\t//evil.example", false)]
    [DataRow("/\\/evil.example", false)]
    public void OnlyLocalReturnUrlsAreAccepted(string? url, bool expected)
        => Assert.AreEqual(expected, AuthenticationExtensions.IsLocalUrl(url));
}
