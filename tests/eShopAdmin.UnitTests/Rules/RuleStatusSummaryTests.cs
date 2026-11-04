using System.Text.Json;
using eShopAdmin.BusinessRules;
using eShopAdmin.Rules;

namespace eShopAdmin.UnitTests.Rules;

[TestClass]
public class RuleStatusSummaryTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private static RuleVersionDto Version(
        int number,
        RuleVersionStatus status,
        DateTimeOffset from,
        DateTimeOffset? to = null,
        string ruleTypeId = "ORD-001")
    {
        using var document = JsonDocument.Parse("""{"minutes":10}""");
        return new RuleVersionDto(
            Guid.NewGuid(), ruleTypeId, "default", number, document.RootElement.Clone(), from, to, status,
            "creator", from, status == RuleVersionStatus.Draft ? null : "publisher",
            status == RuleVersionStatus.Draft ? null : from);
    }

    [TestMethod]
    public void CurrentIsLatestEffectivePublishedVersion()
    {
        var v1 = Version(1, RuleVersionStatus.Published, Now.AddDays(-2));
        var v2 = Version(2, RuleVersionStatus.Published, Now.AddDays(-1));
        var v3 = Version(3, RuleVersionStatus.Published, Now.AddDays(1));

        var summary = RuleStatusSummary.From([v1, v2, v3], Now);

        Assert.AreEqual(v2, summary.Current);
        Assert.AreEqual(v3, summary.NextScheduled);
        Assert.AreEqual(0, summary.DraftCount);
    }

    [TestMethod]
    public void ExpiredVersionIsNotCurrent()
    {
        var v1 = Version(1, RuleVersionStatus.Published, Now.AddDays(-2));
        var v2 = Version(2, RuleVersionStatus.Published, Now.AddDays(-1), Now.AddHours(-1));

        var summary = RuleStatusSummary.From([v1, v2], Now);

        Assert.AreEqual(v1, summary.Current);
        Assert.IsNull(summary.NextScheduled);
    }

    [TestMethod]
    public void EffectiveToIsExclusive()
    {
        var v1 = Version(1, RuleVersionStatus.Published, Now.AddDays(-2), Now);

        Assert.IsNull(RuleStatusSummary.From([v1], Now).Current);
    }

    [TestMethod]
    public void EffectiveFromIsInclusive()
    {
        var v1 = Version(1, RuleVersionStatus.Published, Now);

        Assert.AreEqual(v1, RuleStatusSummary.From([v1], Now).Current);
    }

    [TestMethod]
    public void DraftsAndSupersededAreNeverCurrent()
    {
        var published = Version(1, RuleVersionStatus.Published, Now.AddDays(-3));
        var superseded = Version(2, RuleVersionStatus.Superseded, Now.AddDays(-2));
        var draftA = Version(3, RuleVersionStatus.Draft, Now.AddDays(-1));
        var draftB = Version(4, RuleVersionStatus.Draft, Now.AddDays(1));

        var summary = RuleStatusSummary.From([published, superseded, draftA, draftB], Now);

        Assert.AreEqual(published, summary.Current);
        Assert.IsNull(summary.NextScheduled);
        Assert.AreEqual(2, summary.DraftCount);
    }

    [TestMethod]
    public void NoPublishedVersionMeansDefault()
    {
        var draft = Version(1, RuleVersionStatus.Draft, Now.AddDays(-1));

        var summary = RuleStatusSummary.From([draft], Now);

        Assert.IsNull(summary.Current);
        Assert.IsNull(summary.NextScheduled);
        Assert.AreEqual(1, summary.DraftCount);
        Assert.IsNull(RuleStatusSummary.From([], Now).Current);
    }

    [TestMethod]
    public void TieOnEffectiveFromPicksHighestVersion()
    {
        var from = Now.AddDays(-1);
        var v1 = Version(1, RuleVersionStatus.Published, from);
        var v3 = Version(3, RuleVersionStatus.Published, from);
        var v2 = Version(2, RuleVersionStatus.Published, from);

        Assert.AreEqual(v3, RuleStatusSummary.From([v1, v3, v2], Now).Current);
    }

    [TestMethod]
    public void NextScheduledIsTheEarliestFutureStart()
    {
        var later = Version(2, RuleVersionStatus.Published, Now.AddDays(3));
        var sooner = Version(3, RuleVersionStatus.Published, Now.AddDays(1));

        Assert.AreEqual(sooner, RuleStatusSummary.From([later, sooner], Now).NextScheduled);
    }

    [TestMethod]
    public void UnknownRuleTypeIsListedReadOnly()
    {
        Assert.IsFalse(RuleForms.IsSupported("XYZ-001"));

        using var document = JsonDocument.Parse("""{"anything":1}""");
        Assert.AreEqual("Custom value", ParameterSummary.Describe("XYZ-001", document.RootElement));
    }
}
