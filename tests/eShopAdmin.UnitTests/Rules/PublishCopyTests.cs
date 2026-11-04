using eShopAdmin.Rules;

namespace eShopAdmin.UnitTests.Rules;

[TestClass]
public class PublishCopyTests
{
    [TestMethod]
    public void CopyMatchesThePlan()
    {
        Assert.AreEqual("A start time in the past takes effect as soon as the version is published.", PublishCopy.PastStart);
        Assert.AreEqual("This rule changes prices. Shoppers see the new prices within 60 seconds of it taking effect.", PublishCopy.PriceWarning);
        Assert.AreEqual("Older app versions placed orders in the last 7 days and may show out-of-date prices.", PublishCopy.Guard);
        Assert.AreEqual("Reason for publishing anyway (required, up to 500 characters)", PublishCopy.ReasonLabel);
        Assert.AreEqual("Publishing is paused because app usage couldn't be checked. Try again in a few minutes.", PublishCopy.GuardUnavailable);
        Assert.AreEqual("Another publish happened at the same time. Reload this page and try again.", PublishCopy.ConcurrentPublish);
    }

    [TestMethod]
    public void PublishedCopyNamesTheSnapshot()
    {
        Assert.AreEqual(
            "Published. Rules snapshot 42. Shoppers see the change within 60 seconds of it taking effect.",
            PublishCopy.Published(42));
    }

    [TestMethod]
    public void OtherConflictCarriesTheApiDetail()
    {
        Assert.AreEqual(
            "This version can't be changed any more: Only a draft can be published.",
            PublishCopy.Conflict("Only a draft can be published."));
    }

    [TestMethod]
    public void ReasonIsTrimmed()
    {
        Assert.AreEqual("Black Friday fix", PublishCopy.NormalizeReason("  Black Friday fix \r\n"));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   \r\n\t")]
    public void EmptyReasonIsNeverSent(string? reason)
    {
        Assert.IsNull(PublishCopy.NormalizeReason(reason));
    }
}
