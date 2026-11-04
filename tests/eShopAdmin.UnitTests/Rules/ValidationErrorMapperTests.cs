using eShopAdmin.Rules;

namespace eShopAdmin.UnitTests.Rules;

[TestClass]
public class ValidationErrorMapperTests
{
    [TestMethod]
    public void MapsJsonPathErrorsToFormFields()
    {
        var mapped = ValidationErrorMapper.Map("PRC-001", new Dictionary<string, string[]>
        {
            ["Parameters"] = ["entries[2].value: percent off must be between 0 and 90."],
            ["EffectiveTo"] = ["must be after EffectiveFrom"],
        });

        CollectionAssert.AreEqual(new[] { "percent off must be between 0 and 90." }, mapped.ByField["PriceAdjustment.Entries[2].Value"].ToArray());
        Assert.IsTrue(mapped.ByField.ContainsKey("EffectiveToUtc"));
        Assert.IsEmpty(mapped.Summary);
    }

    [TestMethod]
    [DataRow("PRC-001", "entries[0].target.id: must be a positive id when kind is Type.", "PriceAdjustment.Entries[0].Target.Id")]
    [DataRow("PRC-001", "entries[1].target: duplicates entries[0].target.", "PriceAdjustment.Entries[1].Target")]
    [DataRow("PRC-001", "entries: at most 500 entries are allowed.", "PriceAdjustment.Entries")]
    [DataRow("PRM-001", "linePromotions[3].label: must not contain '<' or '>'.", "Promotion.LinePromotions[3].Label")]
    [DataRow("PRM-001", "linePromotions[0].minUnits: must be 1 or more.", "Promotion.LinePromotions[0].MinUnits")]
    [DataRow("PRM-001", "orderPromotions[12].minSubtotal: must have at most 2 decimal places.", "Promotion.OrderPromotions[12].MinSubtotal")]
    [DataRow("SHP-001", "fee: must be between 0 and 100.", "ShippingFee.Fee")]
    [DataRow("SHP-001", "freeShippingThreshold: must be 0 or more.", "ShippingFee.FreeShippingThreshold")]
    [DataRow("ORD-001", "minutes: must be between 1 and 60.", "GracePeriod.Minutes")]
    [DataRow("ORD-013", "policy: must be AllOrNothing or PartialFulfilment.", "ShortfallPolicy.Policy")]
    public void MapsEachRuleTypeToItsFormProperty(string ruleTypeId, string message, string field)
    {
        var mapped = ValidationErrorMapper.Map(ruleTypeId, new Dictionary<string, string[]> { ["Parameters"] = [message] });

        Assert.IsTrue(mapped.ByField.ContainsKey(field), string.Join(", ", mapped.ByField.Keys));
        Assert.AreEqual(message[(message.IndexOf(": ", StringComparison.Ordinal) + 2)..], mapped.ByField[field].Single());
                Assert.IsEmpty(mapped.Summary);
    }

    [TestMethod]
    public void GroupsSeveralMessagesForTheSameField()
    {
        var mapped = ValidationErrorMapper.Map("PRC-001", new Dictionary<string, string[]>
        {
            ["Parameters"] =
            [
                "entries[0].value: fixed price must be between 0.01 and 1000000.",
                "entries[0].value: fixed price must have at most 2 decimal places.",
                "entries[1].value: percent off must be between 0 and 90.",
            ],
        });

        Assert.HasCount(2, mapped.ByField["PriceAdjustment.Entries[0].Value"]);
        Assert.HasCount(1, mapped.ByField["PriceAdjustment.Entries[1].Value"]);
    }

    [TestMethod]
    public void MapsEffectiveFromAndTo()
    {
        var mapped = ValidationErrorMapper.Map("ORD-001", new Dictionary<string, string[]>
        {
            ["EffectiveFrom"] = ["bad from"],
            ["EffectiveTo"] = ["bad to"],
        });

        CollectionAssert.AreEqual(new[] { "bad from" }, mapped.ByField["EffectiveFromUtc"].ToArray());
        CollectionAssert.AreEqual(new[] { "bad to" }, mapped.ByField["EffectiveToUtc"].ToArray());
                Assert.IsEmpty(mapped.Summary);
    }

    [TestMethod]
    public void UnmappableErrorsGoToSummary()
    {
        var mapped = ValidationErrorMapper.Map("PRC-001", new Dictionary<string, string[]>
        {
            ["Parameters"] = ["Parameters are required."],
            ["RuleTypeId"] = ["Rule type 'XYZ-001' is not registered."],
            ["Scope"] = ["Scope must be 'default'."],
        });

        Assert.IsEmpty(mapped.ByField);
        CollectionAssert.AreEquivalent(
            new[] { "Parameters are required.", "Rule type 'XYZ-001' is not registered.", "Scope must be 'default'." },
            mapped.Summary.ToArray());
    }

    [TestMethod]
    [DataRow("Parameters don't match the PRC-001 schema: The JSON value could not be converted.")]
    [DataRow("'minutes' must be between 1 and 60.")]
    [DataRow("Entries[0].value: starts with a capital")]
    [DataRow("entries[0].value:no space after the colon")]
    [DataRow("entries[x].value: bad index")]
    public void ParametersMessagesWithoutAJsonPathGoToSummary(string message)
    {
        var mapped = ValidationErrorMapper.Map("PRC-001", new Dictionary<string, string[]> { ["Parameters"] = [message] });

        Assert.IsEmpty(mapped.ByField);
        CollectionAssert.AreEqual(new[] { message }, mapped.Summary.ToArray());
    }

    [TestMethod]
    public void PathErrorsForAnUnsupportedRuleTypeKeepTheirFullMessageInSummary()
    {
        var mapped = ValidationErrorMapper.Map("XYZ-001", new Dictionary<string, string[]> { ["Parameters"] = ["foo: bad"] });

        Assert.IsEmpty(mapped.ByField);
        CollectionAssert.AreEqual(new[] { "foo: bad" }, mapped.Summary.ToArray());
    }
}
