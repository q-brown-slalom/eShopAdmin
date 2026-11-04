using eShopAdmin.Rules;

namespace eShopAdmin.UnitTests.Rules;

[TestClass]
public class StatusBannerTests
{
    [TestMethod]
    public void DraftSavedCodeRendersServerCopy()
        => Assert.AreEqual("Draft v3 saved.", StatusBanner.Text("draft-saved", "3", null));

    [TestMethod]
    public void PublishedCodeRendersThePublishedCopy()
        => Assert.AreEqual(PublishCopy.Published(12), StatusBanner.Text("published", null, "12"));

    [TestMethod]
    public void QueriesUseFixedCodes()
    {
        Assert.AreEqual("status=draft-saved&v=3", StatusBanner.DraftSavedQuery(3));
        Assert.AreEqual("status=published&snapshot=12", StatusBanner.PublishedQuery(12));
    }

    [TestMethod]
    [DataRow(null, null, null)]
    [DataRow("", "3", "3")]
    [DataRow("Draft v3 saved.", "3", "3")]
    [DataRow("Published. You are now owed a prize.", null, "12")]
    [DataRow("DRAFT-SAVED", "3", null)]
    [DataRow("<b>x</b>", "3", "3")]
    public void UnknownCodesRenderNothing(string? code, string? version, string? snapshot)
        => Assert.IsNull(StatusBanner.Text(code, version, snapshot));

    [TestMethod]
    [DataRow("draft-saved", null, "5")]
    [DataRow("draft-saved", "", null)]
    [DataRow("draft-saved", "-1", null)]
    [DataRow("draft-saved", "+3", null)]
    [DataRow("draft-saved", " 3", null)]
    [DataRow("draft-saved", "3.5", null)]
    [DataRow("draft-saved", "abc", null)]
    [DataRow("draft-saved", "99999999999999999999", null)]
    [DataRow("published", "5", null)]
    [DataRow("published", null, "-12")]
    [DataRow("published", null, "1e3")]
    [DataRow("published", null, "<script>")]
    public void MissingOrMalformedNumbersRenderNothing(string code, string? version, string? snapshot)
        => Assert.IsNull(StatusBanner.Text(code, version, snapshot));

    [TestMethod]
    public void ZeroIsAValidNumber()
        => Assert.AreEqual("Draft v0 saved.", StatusBanner.Text("draft-saved", "0", null));
}
