using eShopAdmin.BusinessRules;
using eShopAdmin.Rules;

namespace eShopAdmin.UnitTests.Rules;

[TestClass]
public class AuditLogViewTests
{
    [TestMethod]
    [DataRow(null, 0)]
    [DataRow("", 0)]
    [DataRow("0", 0)]
    [DataRow("3", 3)]
    [DataRow("-1", 0)]
    [DataRow("abc", 0)]
    [DataRow("1.5", 0)]
    [DataRow("99999999999999999999", 0)]
    [DataRow("2000000000", AuditLogView.MaxPage)]
    public void PageIsZeroBasedAndGarbageFallsBackToTheFirstPage(string? raw, int expected)
    {
        Assert.AreEqual(expected, AuditLogView.ParsePage(raw));
    }

    [TestMethod]
    [DataRow(0, 20, 0L, false)]
    [DataRow(0, 20, 20L, false)]
    [DataRow(0, 20, 21L, true)]
    [DataRow(1, 20, 40L, false)]
    [DataRow(1, 20, 41L, true)]
    [DataRow(5, 20, 41L, false)]
    public void NextPageExistsOnlyWhenMoreEntriesFollow(int pageIndex, int pageSize, long count, bool expected)
    {
        var page = new PaginatedItems<AuditEntryDto>(pageIndex, pageSize, count, []);

        Assert.AreEqual(expected, AuditLogView.HasNext(page));
    }

    [TestMethod]
    [DataRow(0L, 20, 0)]
    [DataRow(1L, 20, 1)]
    [DataRow(20L, 20, 1)]
    [DataRow(21L, 20, 2)]
    public void CountsPages(long count, int pageSize, int expected)
    {
        Assert.AreEqual(expected, AuditLogView.PageCount(count, pageSize));
    }

    [TestMethod]
    public void ByMarksTheSignedInUser()
    {
        Assert.AreEqual("alice-id (you)", AuditLogView.By("alice-id", "alice-id"));
        Assert.AreEqual("bob-id", AuditLogView.By("bob-id", "alice-id"));
        Assert.AreEqual("alice-id", AuditLogView.By("alice-id", "Alice-id"));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void ByNeverMarksWhenThereIsNoSignedInUserId(string? subject)
    {
        Assert.AreEqual("", AuditLogView.By("", subject));
        Assert.AreEqual("alice-id", AuditLogView.By("alice-id", subject));
    }

    [TestMethod]
    public void JsonIsPrettyPrinted()
    {
        Assert.AreEqual("{\n  \"minutes\": 10\n}", AuditLogView.PrettyJson("{\"minutes\":10}"));
    }

    [TestMethod]
    [DataRow("{\"minutes\":")]
    [DataRow("not json")]
    [DataRow("{} trailing")]
    public void MalformedJsonIsShownAsRecorded(string json)
    {
        Assert.AreEqual(json, AuditLogView.PrettyJson(json));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("  ")]
    public void MissingJsonIsShownAsNone(string? json)
    {
        Assert.AreEqual("(none)", AuditLogView.PrettyJson(json));
    }
}
