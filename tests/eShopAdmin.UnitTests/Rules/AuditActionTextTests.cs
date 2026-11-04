using eShopAdmin.BusinessRules;
using eShopAdmin.Rules;

namespace eShopAdmin.UnitTests.Rules;

[TestClass]
public class AuditActionTextTests
{
    [TestMethod]
    [DataRow(AuditAction.DraftCreated, "Draft created")]
    [DataRow(AuditAction.DraftUpdated, "Draft updated")]
    [DataRow(AuditAction.Published, "Published")]
    [DataRow(AuditAction.GuardOverridden, "Guard overridden")]
    public void DescribesEveryAuditAction(AuditAction action, string expected)
    {
        Assert.AreEqual(expected, AuditActionText.Describe(action));
    }

    [TestMethod]
    public void EveryAuditActionHasItsOwnText()
    {
        var actions = Enum.GetValues<AuditAction>();

        Assert.HasCount(4, actions, "A new audit action needs its text and a row in DescribesEveryAuditAction.");
        Assert.AreEqual(actions.Length, actions.Select(AuditActionText.Describe).Distinct().Count());
    }
}
